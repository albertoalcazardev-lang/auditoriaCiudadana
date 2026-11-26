import { useId, useMemo, useRef, useState, type FocusEvent, type KeyboardEvent } from 'react';
import { ChevronDown } from 'lucide-react';
import type { AreaReceptoraItem } from '../types/solicitudes.types';

interface AreaReceptoraComboboxProps {
  id: string;
  options: AreaReceptoraItem[];
  value: string;
  disabled?: boolean;
  error?: string;
  errorId: string;
  inputRef: (element: HTMLInputElement | null) => void;
  onChange: (areaId: string) => void;
  onBlur: () => void;
}

function normalizeText(text: string): string {
  return text
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim();
}

export function AreaReceptoraCombobox({
  id,
  options,
  value,
  disabled = false,
  error,
  errorId,
  inputRef,
  onChange,
  onBlur,
}: AreaReceptoraComboboxProps) {
  const listboxId = useId();
  const containerRef = useRef<HTMLDivElement | null>(null);
  const localInputRef = useRef<HTMLInputElement | null>(null);

  const [isOpen, setIsOpen] = useState(false);
  const [isFiltering, setIsFiltering] = useState(false);
  const [query, setQuery] = useState('');
  const [activeIndex, setActiveIndex] = useState(-1);

  const selectedOption = useMemo(
    () => options.find((option) => option.id === value) ?? null,
    [options, value],
  );

  const displayValue = isFiltering ? query : (selectedOption?.nombre ?? '');

  const filteredOptions = useMemo(() => {
    if (!isFiltering) return options;
    const normalizedQuery = normalizeText(query);
    if (!normalizedQuery) return options;
    return options.filter((option) => normalizeText(option.nombre).includes(normalizedQuery));
  }, [isFiltering, options, query]);

  const setCombinedRef = (element: HTMLInputElement | null) => {
    localInputRef.current = element;
    inputRef(element);
  };

  const selectOption = (option: AreaReceptoraItem) => {
    onChange(option.id);
    setQuery(option.nombre);
    setIsFiltering(false);
    setIsOpen(false);
    setActiveIndex(-1);
  };

  const handleContainerBlur = (event: FocusEvent<HTMLDivElement>) => {
    const nextTarget = event.relatedTarget as Node | null;
    if (nextTarget && containerRef.current?.contains(nextTarget)) {
      return;
    }
    setIsOpen(false);
    setIsFiltering(false);
    setActiveIndex(-1);
    onBlur();
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (disabled) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      if (!isOpen) {
        setIsOpen(true);
        const selectedIdx = filteredOptions.findIndex((option) => option.id === value);
        setActiveIndex(selectedIdx >= 0 ? selectedIdx : filteredOptions.length > 0 ? 0 : -1);
        return;
      }
      if (filteredOptions.length > 0) {
        setActiveIndex((prev) => (prev + 1) % filteredOptions.length);
      }
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      if (!isOpen) {
        setIsOpen(true);
        setActiveIndex(filteredOptions.length > 0 ? filteredOptions.length - 1 : -1);
        return;
      }
      if (filteredOptions.length > 0) {
        setActiveIndex((prev) => (prev <= 0 ? filteredOptions.length - 1 : prev - 1));
      }
      return;
    }

    if (event.key === 'Enter' && isOpen) {
      event.preventDefault();
      const targetOption =
        activeIndex >= 0 ? filteredOptions[activeIndex] : filteredOptions[0];
      if (targetOption) {
        selectOption(targetOption);
      }
      return;
    }

    if (event.key === 'Escape' && isOpen) {
      event.preventDefault();
      setIsOpen(false);
      setIsFiltering(false);
      setQuery(selectedOption?.nombre ?? '');
      setActiveIndex(-1);
    }
  };

  const activeOptionId =
    isOpen && activeIndex >= 0 && filteredOptions[activeIndex]
      ? `${listboxId}-option-${filteredOptions[activeIndex].id}`
      : undefined;

  return (
    <div ref={containerRef} className="solicitud-combobox" onBlur={handleContainerBlur}>
      <div className="solicitud-input-wrap">
        <input
          ref={setCombinedRef}
          id={id}
          type="text"
          role="combobox"
          autoComplete="off"
          aria-autocomplete="list"
          aria-expanded={isOpen}
          aria-controls={listboxId}
          aria-activedescendant={activeOptionId}
          aria-invalid={Boolean(error)}
          aria-describedby={error ? errorId : undefined}
          disabled={disabled}
          className="solicitud-input has-right-button"
          placeholder="Busca o selecciona un área o dependencia"
          value={displayValue}
          onFocus={() => {
            if (disabled) return;
            setIsOpen(true);
            const selectedIdx = filteredOptions.findIndex((option) => option.id === value);
            setActiveIndex(selectedIdx >= 0 ? selectedIdx : -1);
          }}
          onChange={(event) => {
            const nextText = event.target.value;
            setIsFiltering(true);
            setQuery(nextText);
            setIsOpen(true);
            setActiveIndex(0);
            if (value && nextText !== selectedOption?.nombre) {
              onChange('');
            }
          }}
          onKeyDown={handleKeyDown}
        />

        <button
          type="button"
          tabIndex={-1}
          disabled={disabled}
          aria-label="Mostrar opciones de área o dependencia receptora"
          className="solicitud-combobox-toggle"
          onMouseDown={(event) => event.preventDefault()}
          onClick={() => {
            if (disabled) return;
            const nextOpen = !isOpen;
            setIsOpen(nextOpen);
            if (nextOpen) {
              localInputRef.current?.focus();
            }
          }}
        >
          <ChevronDown size={16} aria-hidden="true" />
        </button>
      </div>

      {isOpen && !disabled && (
        <ul id={listboxId} role="listbox" className="solicitud-combobox-list">
          {filteredOptions.length === 0 ? (
            <li className="solicitud-combobox-empty">No se encontraron áreas coincidentes.</li>
          ) : (
            filteredOptions.map((option, index) => {
              const isSelected = option.id === value;
              const isActive = index === activeIndex;
              return (
                <li
                  key={option.id}
                  id={`${listboxId}-option-${option.id}`}
                  role="option"
                  aria-selected={isSelected}
                  className={`solicitud-combobox-option ${isActive ? 'is-active' : ''}`}
                  onMouseDown={(event) => event.preventDefault()}
                  onMouseEnter={() => setActiveIndex(index)}
                  onClick={() => selectOption(option)}
                >
                  {option.nombre}
                </li>
              );
            })
          )}
        </ul>
      )}
    </div>
  );
}
