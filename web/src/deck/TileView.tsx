import { useEffect, useRef } from 'react'
import { Icon } from '../icons'
import type { Theme, TileState } from '../protocol'

interface TileViewProps {
  tile: TileState
  theme: Theme
  pressed?: boolean
  /** Slider value shown while the user drags (overrides the server value). */
  dragValue?: number | null
  selected?: boolean
}

/**
 * Pure visual of a tile, shared by the deck and the editor preview. Sizes use container query units
 * so a tile looks the same at any size: fontSize 12 means "12% of a 100px tile".
 */
export function TileView({ tile, theme, pressed, dragValue, selected }: TileViewProps) {
  const hasIcon = !!tile.icon
  const textPosition = tile.textPosition ?? (hasIcon || tile.image ? 'bottom' : 'center')
  const classes = ['tile', `tile-${tile.kind}`, `text-${textPosition}`]
  if (pressed) classes.push('pressed')
  if (selected) classes.push('selected')
  if (tile.image) classes.push('has-image')

  const style: React.CSSProperties = {
    background: tile.background ?? theme.tileBackground,
    color: tile.textColor ?? theme.textColor,
    borderRadius: theme.radius,
  }

  return (
    <div className={classes.join(' ')} style={style}>
      {tile.image && (
        <img className="tile-image" src={tile.image} alt="" draggable={false} style={{ objectFit: tile.imageFit ?? 'cover' }} />
      )}
      {tile.kind === 'slider' && tile.slider && <SliderFill slider={tile.slider} value={dragValue ?? tile.slider.value} />}
      {tile.kind === 'widget' && tile.widget?.type === 'graph' && <Graph widget={tile.widget} />}
      {tile.kind === 'widget' && tile.widget?.type === 'gauge' && <Gauge widget={tile.widget} />}
      <div className="tile-content">
        {hasIcon && !(tile.image && textPosition === 'center') && (
          <Icon name={tile.icon!} className="tile-icon" style={{ color: tile.iconColor ?? undefined }} />
        )}
        {tile.text && (
          <span className="tile-text" style={tile.fontSize ? { fontSize: `${tile.fontSize}cqmin` } : undefined}>
            {tile.text}
          </span>
        )}
      </div>
    </div>
  )
}

function SliderFill({ slider, value }: { slider: NonNullable<TileState['slider']>; value: number }) {
  const range = slider.max - slider.min || 1
  const percent = ((value - slider.min) / range) * 100
  const vertical = slider.orientation === 'vertical'
  return (
    <div className="slider-track">
      <div
        className="slider-fill"
        style={{
          background: slider.color ?? 'var(--accent)',
          [vertical ? 'height' : 'width']: `${percent}%`,
          ...(vertical ? { left: 0, right: 0, bottom: 0 } : { top: 0, bottom: 0, left: 0 }),
        }}
      />
    </div>
  )
}

function Graph({ widget }: { widget: NonNullable<TileState['widget']> }) {
  const canvas = useRef<HTMLCanvasElement>(null)
  const series = widget.series

  useEffect(() => {
    const el = canvas.current
    if (!el) return
    const { width, height } = el.getBoundingClientRect()
    const ratio = devicePixelRatio || 1
    el.width = Math.max(1, width * ratio)
    el.height = Math.max(1, height * ratio)
    const ctx = el.getContext('2d')
    if (!ctx) return
    ctx.scale(ratio, ratio)
    ctx.clearRect(0, 0, width, height)
    if (!series || series.length < 2) return

    const min = widget.min
    const max = widget.max > widget.min ? widget.max : Math.max(...series, widget.min + 1)
    const step = width / (Math.max(series.length, 2) - 1)
    const y = (v: number) => height - ((Math.min(max, Math.max(min, v)) - min) / (max - min)) * height * 0.9
    const color = widget.color ?? '#5b8cff'

    ctx.beginPath()
    series.forEach((v, i) => (i === 0 ? ctx.moveTo(0, y(v)) : ctx.lineTo(i * step, y(v))))
    ctx.strokeStyle = color
    ctx.lineWidth = 2
    ctx.lineJoin = 'round'
    ctx.stroke()
    ctx.lineTo((series.length - 1) * step, height)
    ctx.lineTo(0, height)
    ctx.closePath()
    ctx.globalAlpha = 0.25
    ctx.fillStyle = color
    ctx.fill()
  }, [series, widget.min, widget.max, widget.color])

  return <canvas ref={canvas} className="tile-graph" />
}

function Gauge({ widget }: { widget: NonNullable<TileState['widget']> }) {
  const range = widget.max - widget.min || 1
  const ratio = Math.min(1, Math.max(0, ((widget.value ?? widget.min) - widget.min) / range))
  // 270° arc starting at the bottom-left.
  const radius = 40
  const circumference = 2 * Math.PI * radius
  const arc = circumference * 0.75
  return (
    <svg className="tile-gauge" viewBox="0 0 100 100">
      <circle cx="50" cy="50" r={radius} className="gauge-track" strokeDasharray={`${arc} ${circumference}`} transform="rotate(135 50 50)" />
      <circle
        cx="50"
        cy="50"
        r={radius}
        className="gauge-value"
        stroke={widget.color ?? 'var(--accent)'}
        strokeDasharray={`${arc * ratio} ${circumference}`}
        transform="rotate(135 50 50)"
      />
    </svg>
  )
}
