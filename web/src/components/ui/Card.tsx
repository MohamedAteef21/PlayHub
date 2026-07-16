import { type HTMLAttributes } from 'react';

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  hover?: boolean;
}

export function Card({ hover = true, className = '', children, ...props }: CardProps) {
  return (
    <div
      className={`rounded-xl border border-border bg-surface-elevated p-4 animate-fade-in ${hover ? 'transition-all duration-300 hover:-translate-y-0.5 hover:border-primary/40 hover:shadow-lg hover:shadow-primary/5' : ''} ${className}`}
      {...props}
    >
      {children}
    </div>
  );
}

export function CardHeader({ className = '', children }: { className?: string; children: React.ReactNode }) {
  return <div className={`mb-3 flex items-center justify-between ${className}`}>{children}</div>;
}

export function CardTitle({ className = '', children }: { className?: string; children: React.ReactNode }) {
  return <h3 className={`font-semibold text-text ${className}`}>{children}</h3>;
}
