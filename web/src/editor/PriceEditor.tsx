import type { Parameter, Price, PriceType } from '@/api';
import NumberField from '@/components/NumberField';
import SelectField from '@/components/SelectField';
import { PRICE_LABELS, describePrice, numericParameters } from '@/editor/model';
import styles from '@/editor/Editor.module.css';

interface PriceEditorProps {
	id: string;
	price: Price | null;
	parameters: Parameter[];
	onChange: (price: Price | null) => void;
}

type Kind = PriceType | 'none';
const KINDS: Kind[] = ['none', 'fixed', 'percent', 'perUnit'];

export default function PriceEditor({ id, price, parameters, onChange }: PriceEditorProps) {
	const numeric = numericParameters(parameters);

	function changeKind(kind: Kind) {
		if (kind === 'none') return onChange(null);
		const base = price ?? { type: kind, amount: 0, rate: 0, param: null };
		const param = kind === 'perUnit' ? (base.param ?? numeric[0]?.id ?? null) : base.param;
		onChange({ ...base, type: kind, param });
	}

	return (
		<fieldset className={styles.fieldset}>
			<legend className={styles.legend}>Prix de l'option</legend>

			<div className={styles.fields}>
				<SelectField
					id={`${id}-kind`}
					label="Type de prix"
					value={price?.type ?? 'none'}
					choices={KINDS}
					labelOf={(k) => PRICE_LABELS[k as Kind]}
					onChange={(k) => changeKind(k as Kind)}
				/>

				{price?.type === 'fixed' && (
					<NumberField
						id={`${id}-amount`}
						label="Montant (EUR)"
						value={price.amount}
						min={0}
						onChange={(v) => onChange({ ...price, amount: v ?? 0 })}
					/>
				)}

				{price?.type === 'percent' && (
					<NumberField
						id={`${id}-rate`}
						label="Taux (% du prix de base)"
						value={price.rate}
						min={0}
						max={100}
						onChange={(v) => onChange({ ...price, rate: v ?? 0 })}
					/>
				)}

				{price?.type === 'perUnit' && (
					<>
						<NumberField
							id={`${id}-rate`}
							label="Prix par unité (EUR)"
							value={price.rate}
							onChange={(v) => onChange({ ...price, rate: v ?? 0 })}
						/>
						<SelectField
							id={`${id}-param`}
							label="Multiplié par"
							value={price.param ?? ''}
							choices={numeric.map((p) => p.id)}
							labelOf={(pid) => numeric.find((p) => p.id === pid)?.label ?? pid}
							onChange={(pid) => onChange({ ...price, param: pid })}
						/>
					</>
				)}
			</div>

			<p className={styles.summary}>
				Sur le devis : <strong>{describePrice(price, parameters)}</strong>
			</p>
		</fieldset>
	);
}
