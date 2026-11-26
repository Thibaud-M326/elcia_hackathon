import styles from '@/components/Field.module.css';

interface SelectFieldProps {
	id: string;
	label: string;
	value: string;
	choices: string[];
	labelOf?: (choice: string) => string;
	onChange: (value: string) => void;
}

export default function SelectField({ id, label, value, choices, labelOf, onChange }: SelectFieldProps) {
	return (
		<div className={styles.field}>
			<label htmlFor={id} className={styles.label}>
				{label}
			</label>
			<select
				id={id}
				value={value}
				className={`${styles.control} ${styles.select}`}
				onChange={(e) => onChange(e.target.value)}
			>
				{choices.map((choice) => (
					<option key={choice} value={choice}>
						{labelOf ? labelOf(choice) : choice}
					</option>
				))}
			</select>
		</div>
	);
}
