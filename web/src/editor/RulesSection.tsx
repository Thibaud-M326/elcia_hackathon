import type { Product, Rule } from '@/api';
import RuleCard from '@/editor/RuleCard';
import { RULE_TYPES, RULE_TYPE_EXAMPLES, RULE_TYPE_LABELS, describeRule, duplicateRule, newRule } from '@/editor/rules';
import styles from '@/editor/Rules.module.css';

interface RulesSectionProps {
	product: Product;
	problems: string[][];
	onChange: (rules: Rule[]) => void;
}

export default function RulesSection({ product, problems, onChange }: RulesSectionProps) {
	const rules = product.rules ?? [];

	function duplicate(index: number) {
		const copy = duplicateRule(rules[index], rules);
		onChange([...rules.slice(0, index + 1), copy, ...rules.slice(index + 1)]);
	}

	function remove(index: number) {
		if (!window.confirm(`Supprimer la règle ${index + 1} ?\n\n${describeRule(rules[index], product)}`)) return;
		onChange(rules.filter((_, i) => i !== index));
	}

	return (
		<div className={styles.section}>
			{rules.length === 0 && <p className={styles.empty}>Aucune règle pour l'instant.</p>}

			{rules.map((rule, index) => (
				<RuleCard
					key={index}
					rule={rule}
					index={index}
					product={product}
					problems={problems[index] ?? []}
					onChange={(next) => onChange(rules.map((r, i) => (i === index ? next : r)))}
					onDuplicate={() => duplicate(index)}
					onDelete={() => remove(index)}
				/>
			))}

			<section className={styles.addBar} aria-labelledby="add-rule-title">
				<h3 id="add-rule-title" className={styles.addTitle}>
					Ajouter une règle
				</h3>
				<div className={styles.addTypes}>
					{RULE_TYPES.map((type) => (
						<button key={type} type="button" className={styles.addType} onClick={() => onChange([...rules, newRule(type, product)])}>
							<span className={styles.addTypeLabel}>{RULE_TYPE_LABELS[type]}</span>
							<span className={styles.addTypeExample}>{RULE_TYPE_EXAMPLES[type]}</span>
						</button>
					))}
				</div>
			</section>
		</div>
	);
}
