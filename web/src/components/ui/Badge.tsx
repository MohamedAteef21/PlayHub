const colors = {
  idle: 'bg-muted/20 text-muted border-muted/30',
  gaming: 'bg-success/20 text-success border-success/30',
  watching: 'bg-accent/20 text-accent border-accent/30',
  paused: 'bg-warning/20 text-warning border-warning/30',
  default: 'bg-primary/20 text-primary border-primary/30',
};

interface BadgeProps {
  status: keyof typeof colors | string;
  children: React.ReactNode;
  pulse?: boolean;
}

export function Badge({ status, children, pulse }: BadgeProps) {
  const key = status.toLowerCase() as keyof typeof colors;
  const color = colors[key] ?? colors.default;
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium ${color} ${pulse ? 'animate-pulse-soft' : ''}`}
    >
      {pulse && <span className="h-1.5 w-1.5 rounded-full bg-current" />}
      {children}
    </span>
  );
}
