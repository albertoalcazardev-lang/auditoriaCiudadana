import { Calendar, ChevronDown } from 'lucide-react';
import type { PeriodoDashboard, PeriodoOption } from '../types/dashboard.types';

interface PeriodSelectProps {
  value: PeriodoDashboard;
  options: readonly PeriodoOption[];
  onChange: (periodo: PeriodoDashboard) => void;
}

export function PeriodSelect({ value, options, onChange }: PeriodSelectProps) {
  return (
    <div className="dashboard-period-wrap">
      <Calendar size={16} aria-hidden="true" className="dashboard-period-icon-left" />
      <select
        aria-label="Periodo"
        value={value}
        onChange={(event) => onChange(event.target.value as PeriodoDashboard)}
        className="dashboard-period-select"
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      <ChevronDown size={15} aria-hidden="true" className="dashboard-period-icon-right" />
    </div>
  );
}
