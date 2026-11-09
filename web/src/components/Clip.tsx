export function Clip({ n }: { n: number }) {
  if (n <= 0) return null
  return <span className="muted" role="img" aria-label={`${n} document${n === 1 ? '' : 's'}`}> 📎 {n}</span>
}
