import Badge from '@/components/Badge';
import styles from '@/components/OptionItem.module.css';

interface OptionItemProps {
	id: string;
	label: string;
	checked: boolean;
	available: boolean;
	autoAdded: boolean;
	autoAddedByLabel?: string;
	reasons: string[];
	amount?: string;
	onChange: (checked: boolean) => void;
}

export default function OptionItem({
	id, label, checked, available, autoAdded, autoAddedByLabel, reasons, amount, onChange,
}: OptionItemProps) {
	const disabled = autoAdded || (!available && !checked);
	const reasonsId = `${id}-reasons`;
	const amountId = `${id}-amount`;
	const autoReasonId = `${id}-auto-reason`;
	const showAutoReason = autoAdded && autoAddedByLabel;
	const descriptions: string[] = [];
	if (amount) descriptions.push(amountId);
	if (showAutoReason) descriptions.push(autoReasonId);
	if (reasons.length > 0) descriptions.push(reasonsId);

	return (
		<li className={`${styles.item}${available ? '' : ` ${styles.unavailable}`}`}>
			<div className={styles.row}>
				<input
					id={id}
					type="checkbox"
					className={styles.checkbox}
					checked={checked}
					disabled={disabled}
					aria-describedby={descriptions.join(' ') || undefined}
					onChange={(e) => onChange(e.target.checked)}
				/>
				<label htmlFor={id} className={styles.label}>
					{label}
				</label>
				{autoAdded && <Badge tone="info">Auto</Badge>}
				{!available && <Badge tone="warning">Indisponible</Badge>}
				{amount && <span id={amountId} className={styles.amount}>{amount}</span>}
			</div>
			{showAutoReason && (
				<p id={autoReasonId} className={styles.autoReason}>Ajouté par {autoAddedByLabel}</p>
			)}
			{reasons.length > 0 && (
				<ul id={reasonsId} className={styles.reasons}>
					{reasons.map((reason) => (
						<li key={reason}>{reason}</li>
					))}
				</ul>
			)}
		</li>
	);
}
