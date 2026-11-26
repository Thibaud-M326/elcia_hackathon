import type { Condition, Op, Parameter, Product, Value } from '@/api';
import Button from '@/components/Button';
import { OP_LABELS, newCondition, opsFor, withOp, withParam } from '@/editor/rules';
import styles from '@/editor/Rules.module.css';

interface ConditionsEditorProps {
	id: string;
	conditions: Condition[];
	product: Product;
	onChange: (conditions: Condition[]) => void;
}

export default function ConditionsEditor({ id, conditions, product, onChange }: ConditionsEditorProps) {
	const params = product.parameters;
	const set = (index: number, c: Condition) => onChange(conditions.map((old, i) => (i === index ? c : old)));

	return (
		<div className={styles.conditions}>
			{conditions.map((c, index) => {
				const param = params.find((p) => p.id === c.param);
				const rowId = `${id}-c${index}`;
				return (
					<div key={index} className={styles.condition}>
						<span className={styles.word}>{index === 0 ? '' : 'et'}</span>
						<select
							aria-label={`Condition ${index + 1} : paramètre`}
							className={styles.select}
							value={c.param}
							onChange={(e) => set(index, withParam(c, params.find((p) => p.id === e.target.value)))}
						>
							{!param && <option value={c.param}>{c.param || 'Choisir...'}</option>}
							{params.map((p) => (
								<option key={p.id} value={p.id}>
									{p.label}
								</option>
							))}
						</select>
						<select
							aria-label={`Condition ${index + 1} : opérateur`}
							className={styles.select}
							value={c.op}
							onChange={(e) => set(index, withOp(c, e.target.value as Op, param))}
						>
							{!opsFor(param).includes(c.op) && <option value={c.op}>{OP_LABELS[c.op]}</option>}
							{opsFor(param).map((op) => (
								<option key={op} value={op}>
									{OP_LABELS[op]}
								</option>
							))}
						</select>
						<ValueInput id={rowId} index={index} condition={c} param={param} onChange={(value) => set(index, { ...c, value })} />
						<Button
							variant="danger"
							className={styles.small}
							aria-label={`Retirer la condition ${index + 1}`}
							onClick={() => onChange(conditions.filter((_, i) => i !== index))}
						>
							Retirer
						</Button>
					</div>
				);
			})}
			<div>
				<Button className={styles.small} onClick={() => onChange([...conditions, newCondition(product)])}>
					+ condition
				</Button>
			</div>
		</div>
	);
}

interface ValueInputProps {
	id: string;
	index: number;
	condition: Condition;
	param: Parameter | undefined;
	onChange: (value: Value | Value[]) => void;
}

function ValueInput({ id, index, condition, param, onChange }: ValueInputProps) {
	const label = `Condition ${index + 1} : valeur`;
	const { value, op } = condition;

	if (param?.type === 'choice' && op === 'in') {
		const selected = Array.isArray(value) ? value.map(String) : [];
		return (
			<fieldset className={styles.checks} aria-label={label}>
				{(param.choices ?? []).map((choice) => (
					<label key={choice} className={styles.check}>
						<input
							type="checkbox"
							checked={selected.includes(choice)}
							onChange={(e) => onChange(e.target.checked ? [...selected, choice] : selected.filter((s) => s !== choice))}
						/>
						{choice}
					</label>
				))}
			</fieldset>
		);
	}

	if (param?.type === 'choice') {
		const current = String(Array.isArray(value) ? (value[0] ?? '') : value);
		const choices = param.choices ?? [];
		return (
			<select aria-label={label} className={styles.select} value={current} onChange={(e) => onChange(e.target.value)}>
				{!choices.includes(current) && <option value={current}>{current || 'Choisir...'}</option>}
				{choices.map((choice) => (
					<option key={choice} value={choice}>
						{choice}
					</option>
				))}
			</select>
		);
	}

	if (param?.type === 'option') {
		return (
			<select aria-label={label} className={styles.select} value={value === true ? 'oui' : 'non'} onChange={(e) => onChange(e.target.value === 'oui')}>
				<option value="oui">cochée</option>
				<option value="non">non cochée</option>
			</select>
		);
	}

	return (
		<input
			id={`${id}-value`}
			aria-label={label}
			type="number"
			inputMode="decimal"
			className={`${styles.select} ${styles.numberInput}`}
			value={typeof value === 'number' ? value : ''}
			onChange={(e) => onChange(Number.isNaN(e.target.valueAsNumber) ? '' : e.target.valueAsNumber)}
		/>
	);
}
