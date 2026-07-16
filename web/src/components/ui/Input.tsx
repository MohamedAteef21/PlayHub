import { type InputHTMLAttributes, forwardRef, useState } from 'react';
import { Icon } from './Icons';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ label, error, className = '', ...props }, ref) => (
    <div className="space-y-1.5">
      {label && <label className="block text-sm font-medium text-muted">{label}</label>}
      <input
        ref={ref}
        className={`w-full rounded-lg border border-border bg-surface px-3 py-2.5 text-text placeholder:text-muted/60 transition-colors focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 ${error ? 'border-danger' : ''} ${className}`}
        {...props}
      />
      {error && <p className="text-xs text-danger">{error}</p>}
    </div>
  )
);
Input.displayName = 'Input';

/** Password field with an eye toggle to show/hide the value. */
export const PasswordInput = forwardRef<HTMLInputElement, Omit<InputProps, 'type'>>(
  ({ label, error, className = '', ...props }, ref) => {
    const [visible, setVisible] = useState(false);
    return (
      <div className="space-y-1.5">
        {label && <label className="block text-sm font-medium text-muted">{label}</label>}
        <div className="relative">
          <input
            ref={ref}
            type={visible ? 'text' : 'password'}
            className={`w-full rounded-lg border border-border bg-surface py-2.5 pe-10 ps-3 text-text placeholder:text-muted/60 transition-colors focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20 ${error ? 'border-danger' : ''} ${className}`}
            {...props}
          />
          <button
            type="button"
            tabIndex={-1}
            aria-label={visible ? 'Hide password' : 'Show password'}
            className="absolute end-2 top-1/2 -translate-y-1/2 rounded p-1 text-muted transition-colors hover:text-text"
            onClick={() => setVisible((v) => !v)}
          >
            <Icon name={visible ? 'eyeOff' : 'eye'} className="h-4 w-4" />
          </button>
        </div>
        {error && <p className="text-xs text-danger">{error}</p>}
      </div>
    );
  }
);
PasswordInput.displayName = 'PasswordInput';
