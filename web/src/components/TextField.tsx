import styles from '@/components/Field.module.css';

interface TextFieldProps {
	id: string;
	label: string;
	value: string;
	hint?: string;
	error?: string | null;
	onChange: (value: string) => void;
}

export default function TextField({ id, label, value, hint, error, onChange }: TextFieldProps) {
	const messageId = `${id}-msg`;

	return (
		<div className={styles.field}>
			<label htmlFor={id} className={styles.label}>
				{label}
			</label>
			<input
				id={id}
				type="text"
				value={value}
				aria-invalid={Boolean(error)}
				aria-describedby={messageId}
				className={`${styles.control}${error ? ` ${styles.invalid}` : ''}`}
				onChange={(e) => onChange(e.target.value)}
			/>
			{error ? (
				<span id={messageId} className={styles.error}>
					{error}
				</span>
			) : (
				<span id={messageId} className={styles.hint}>
					{hint}
				</span>
			)}
		</div>
	);
}
