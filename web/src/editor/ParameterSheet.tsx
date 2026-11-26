import type { Parameter, Product } from '@/api';
import Badge from '@/components/Badge';
import Button from '@/components/Button';
import NumberField from '@/components/NumberField';
import TextField from '@/components/TextField';
import ChoicesEditor from '@/editor/ChoicesEditor';
import PriceEditor from '@/editor/PriceEditor';
import { TYPE_LABELS, usagesOf } from '@/editor/model';
import styles from '@/editor/Editor.module.css';

interface ParameterSheetProps {
	param: Parameter;
	product: Product;
	problems: string[];
	onChange: (param: Parameter) => void;
	onDelete: () => void;
}

const toNull = (v: number | undefined) => (v === undefined ? null : v);

export default function ParameterSheet({ param, product, problems, onChange, onDelete }: ParameterSheetProps) {
	const id = 'sheet';
	const usages = usagesOf(product, param.id);
	const set = (patch: Partial<Parameter>) => onChange({ ...param, ...patch });

	return (
		<section className={styles.sheet} aria-labelledby={`${id}-title`}>
			<div className={styles.sheetHeader}>
				<h2 id={`${id}-title`} className={styles.sheetTitle}>
					{param.label || 'Sans libellé'}
				</h2>
				<Badge tone="info">{TYPE_LABELS[param.type]}</Badge>
			</div>

			{problems.length > 0 && (
				<div className={styles.problems} role="alert">
					<strong>À corriger</strong>
					<ul>
						{problems.map((p) => (
							<li key={p}>{p}</li>
						))}
					</ul>
				</div>
			)}

			<div className={styles.fields}>
				<TextField id={`${id}-label`} label="Libellé (vu par le vendeur)" value={param.label} onChange={(label) => set({ label })} />
				<TextField
					id={`${id}-id`}
					label="Identifiant technique"
					value={param.id}
					hint={usages.length > 0 ? 'Le renommer ne met pas à jour les règles qui l\'utilisent.' : 'Sans espace ni point.'}
					onChange={(value) => set({ id: value })}
				/>
			</div>

			{param.type === 'number' && (
				<div className={styles.fields3}>
					<NumberField
						id={`${id}-min`}
						label="Minimum"
						value={param.min ?? undefined}
						optional
						onChange={(v) => set({ min: toNull(v) })}
					/>
					<NumberField
						id={`${id}-max`}
						label="Maximum"
						value={param.max ?? undefined}
						optional
						error={param.min != null && param.max != null && param.min > param.max ? 'Inférieur au minimum' : null}
						onChange={(v) => set({ max: toNull(v) })}
					/>
					<NumberField
						id={`${id}-default`}
						label="Valeur par défaut"
						value={typeof param.default === 'number' ? param.default : undefined}
						min={param.min ?? undefined}
						max={param.max ?? undefined}
						optional
						onChange={(v) => set({ default: toNull(v) })}
					/>
				</div>
			)}

			{param.type === 'choice' && (
				<ChoicesEditor
					id={`${id}-choices`}
					choices={param.choices ?? []}
					defaultChoice={typeof param.default === 'string' ? param.default : null}
					onChange={(choices, def) => set({ choices, default: def })}
				/>
			)}

			{param.type === 'option' && (
				<>
					<label className={styles.checkboxRow}>
						<input type="checkbox" checked={param.default === true} onChange={(e) => set({ default: e.target.checked })} />
						Cochée par défaut
					</label>
					<PriceEditor id={`${id}-price`} price={param.price ?? null} parameters={product.parameters} onChange={(price) => set({ price })} />
				</>
			)}

			{usages.length > 0 && (
				<div className={styles.usages}>
					<strong>Utilisé par</strong>
					<ul>
						{usages.map((u) => (
							<li key={u}>{u}</li>
						))}
					</ul>
				</div>
			)}

			<div className={styles.sheetFooter}>
				<Button variant="danger" onClick={onDelete}>
					Supprimer ce paramètre
				</Button>
			</div>
		</section>
	);
}
