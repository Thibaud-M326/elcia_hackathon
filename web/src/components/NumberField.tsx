import styles from '@/components/Field.module.css';

interface NumberFieldProps {
	id: string;
	label: string;
	value: number | undefined;
	min?: number;
	max?: number;
	optional?: boolean;
	error?: string | null;
	onChange: (value: number | undefined) => void;
}

function problemOf(value: number | undefined, min?: number, max?: number, optional?: boolean): string | null {
	if (value === undefined) return optional ? null : 'Valeur requise';
	if (min !== undefined && value < min) return `Minimum ${min}`;
	if (max !== undefined && value > max) return `Maximum ${max}`;
	return null;
}

export default function NumberField({ id, label, value, min, max, optional, error, onChange }: NumberFieldProps) {
	const problem = error || problemOf(value, min, max, optional);
	const hint = [min !== undefined && `min ${min}`, max !== undefined && `max ${max}`].filter(Boolean).join(' · ');
	const messageId = `${id}-msg`;

	return (
		<div className={styles.field}>
			<label htmlFor={id} className={styles.label}>
				{label}
			</label>
			<input
				id={id}
				type="number"
				inputMode="decimal"
				min={min}
				max={max}
				value={value ?? ''}
				aria-invalid={problem !== null}
				aria-describedby={messageId}
				className={`${styles.control} ${styles.number}${problem ? ` ${styles.invalid}` : ''}`}
				onChange={(e) => onChange(Number.isNaN(e.target.valueAsNumber) ? undefined : e.target.valueAsNumber)}
			/>
			{problem ? (
				<span id={messageId} className={styles.error}>
					{problem}
				</span>
			) : (
				<span id={messageId} className={styles.hint}>
					{hint}
				</span>
			)}
		</div>
	);
}
