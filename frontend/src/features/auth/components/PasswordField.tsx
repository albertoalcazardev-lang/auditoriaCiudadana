import { useState, type Ref } from 'react';
import { Eye, EyeOff, Lock } from 'lucide-react';

interface PasswordFieldProps {
  value: string;
  onChange: (value: string) => void;
  id: string;
  name?: string;
  label: string;
  visibilityLabel?: string;
  placeholder: string;
  autoComplete: 'new-password' | 'current-password';
  inputRef?: Ref<HTMLInputElement>;
  help?: string;
  error?: string;
}

export function PasswordField({
  id, name, label, visibilityLabel = 'contraseña', placeholder, autoComplete, value, onChange, inputRef, help, error,
}: PasswordFieldProps) {
  const [visible, setVisible] = useState(false);
  const Icon = visible ? EyeOff : Eye;
  return (
    <div>
      <label htmlFor={id}>{label} *</label>
      <div className="login-input-wrap password-input">
        <Lock aria-hidden="true" />
        <input id={id} name={name} ref={inputRef} type={visible ? 'text' : 'password'}
          autoComplete={autoComplete} required value={value}
          onChange={(event) => onChange(event.target.value)} placeholder={placeholder}
          aria-invalid={Boolean(error)}
          aria-describedby={[error && `${id}-error`, help && `${id}-help`].filter(Boolean).join(' ') || undefined} />
        <button type="button" className="login-toggle" aria-controls={id}
          aria-label={`${visible ? 'Ocultar' : 'Mostrar'} ${visibilityLabel}`}
          onClick={() => setVisible(!visible)}>
          <Icon aria-hidden="true" size={18} />
        </button>
      </div>
      {error && <p id={`${id}-error`} className="login-field-error">{error}</p>}
      {help && <p id={`${id}-help`} className="login-help">{help}</p>}
    </div>
  );
}
