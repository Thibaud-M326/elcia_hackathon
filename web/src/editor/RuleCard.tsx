import type { Product, Rule } from '@/api';
import Badge from '@/components/Badge';
import Button from '@/components/Button';
import TextField from '@/components/TextField';
import ConditionsEditor from '@/editor/ConditionsEditor';
import PriceEditor from '@/editor/PriceEditor';
import { RULE_TYPE_LABELS, optionParameters } from '@/editor/rules';
import styles from '@/editor/Rules.module.css';

interface RuleCardProps {
	rule: Rule;
	index: number;
	product: Product;
	problems: string[];
	onChange: (rule: Rule) => void;
	onDuplicate: () => void;
	onDelete: () => void;
}

export default function RuleCard({ rule, index, product, problems, onChange, onDuplicate, onDelete }: RuleCardProps) {
	const id = `rule-${index}`;
	const set = (patch: Partial<Rule>) => onChange({ ...rule, ...patch });
	const options = optionParameters(product);

	const optionSelect = (field: 'option' | 'other', label: string) => {
		const value = rule[field] ?? '';
		return (
			<select aria-label={label} className={styles.select} value={value} onChange={(e) => set(field === 'option' ? { option: e.target.value } : { other: e.target.value })}>
				{!options.some((o) => o.id === value) && <option value={value}>{value || 'Choisir une option...'}</option>}
				{options.map((o) => (
					<option key={o.id} value={o.id}>
						{o.label}
					</option>
				))}
			</select>
		);
	};

	const conditions = <ConditionsEditor id={id} conditions={rule.when} product={product} onChange={(when) => set({ when })} />;

	return (
		<article className={`${styles.card}${problems.length > 0 ? ` ${styles.cardInvalid}` : ''}`} aria-labelledby={`${id}-title`}>
			<header className={styles.cardHeader}>
				<span id={`${id}-title`} className={styles.ruleNumber}>
					Règle {index + 1}
				</span>
				<Badge tone="info">{RULE_TYPE_LABELS[rule.type]}</Badge>
				<input
					aria-label={`Règle ${index + 1} : identifiant`}
					className={`${styles.select} ${styles.idInput}`}
					value={rule.id}
					onChange={(e) => set({ id: e.target.value })}
				/>
				<span className={styles.actions}>
					<Button className={styles.small} onClick={onDuplicate}>
						Dupliquer
					</Button>
					<Button variant="danger" className={styles.small} onClick={onDelete}>
						Supprimer
					</Button>
				</span>
			</header>

			{rule.type === 'availableIf' && (
				<>
					<p className={styles.sentence}>
						{optionSelect('option', `Règle ${index + 1} : option`)}
						<span className={styles.word}>n'est possible que si</span>
					</p>
					{conditions}
				</>
			)}

			{rule.type === 'incompatible' && (
				<p className={styles.sentence}>
					{optionSelect('option', `Règle ${index + 1} : première option`)}
					<span className={styles.word}>est incompatible avec</span>
					{optionSelect('other', `Règle ${index + 1} : seconde option`)}
				</p>
			)}

			{rule.type === 'implies' && (
				<p className={styles.sentence}>
					{optionSelect('option', `Règle ${index + 1} : option`)}
					<span className={styles.word}>entraîne</span>
					{optionSelect('other', `Règle ${index + 1} : option ajoutée`)}
				</p>
			)}

			{rule.type === 'surcharge' && (
				<>
					<p className={styles.sentence}>
						<span className={styles.word}>Si</span>
					</p>
					{conditions}
					<PriceEditor
						id={`${id}-price`}
						legend="ajouter"
						allowFree={false}
						price={rule.price ?? null}
						parameters={product.parameters}
						onChange={(price) => set({ price })}
					/>
					<TextField id={`${id}-label`} label="Libellé sur le devis" value={rule.label ?? ''} onChange={(label) => set({ label })} />
				</>
			)}

			{rule.type !== 'surcharge' && (
				<TextField
					id={`${id}-message`}
					label={rule.type === 'implies' ? 'Message (facultatif)' : 'Message affiché au vendeur'}
					value={rule.message ?? ''}
					hint="{dormant} est remplacé par la valeur saisie."
					onChange={(message) => set({ message: message === '' && rule.type === 'implies' ? null : message })}
				/>
			)}

			{problems.length > 0 && (
				<ul className={styles.problems} role="alert">
					{problems.map((p) => (
						<li key={p}>{p}</li>
					))}
				</ul>
			)}
		</article>
	);
}
