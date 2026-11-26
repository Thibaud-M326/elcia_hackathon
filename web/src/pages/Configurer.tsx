import { useEffect, useState } from 'react';
import { API_URL, getDraft } from '@/api';
import type { ParamType, Parameter, Product, Rule } from '@/api';
import AppLayout from '@/components/AppLayout';
import Badge from '@/components/Badge';
import Button from '@/components/Button';
import ParameterSheet from '@/editor/ParameterSheet';
import RulesSection from '@/editor/RulesSection';
import { REQUIRED_IDS, TYPE_EXAMPLES, TYPE_LABELS, newParameter, parameterProblems, usagesOf } from '@/editor/model';
import { ruleProblems } from '@/editor/rules';
import { navFor } from '@/nav';
import styles from '@/pages/Configurer.module.css';

const NAV = navFor('configurer');
const TYPES: ParamType[] = ['number', 'choice', 'option'];

type Selection = number | 'new' | null;
type Tab = 'parameters' | 'rules';

export default function Configurer({ productId }: { productId: string }) {
	const [saved, setSaved] = useState<Product | null>(null);
	const [product, setProduct] = useState<Product | null>(null);
	const [loadError, setLoadError] = useState<string | null>(null);
	const [selection, setSelection] = useState<Selection>(null);
	const [tab, setTab] = useState<Tab>('parameters');

	useEffect(() => {
		getDraft(productId)
			.then((p) => {
				setSaved(p);
				setProduct(p);
				setSelection(p.parameters.length > 0 ? 0 : null);
			})
			.catch((e: unknown) => {
				const message = e instanceof TypeError ? `API error (${API_URL}).` : String(e instanceof Error ? e.message : e);
				setLoadError(message);
			});
	}, [productId]);

	if (!product) {
		return (
			<AppLayout title="Configurer" nav={NAV}>
				{loadError ? (
					<p className={styles.loadError} role="alert">
						{loadError}
					</p>
				) : (
					<p className={styles.muted}>Chargement du brouillon...</p>
				)}
			</AppLayout>
		);
	}

	const parameters = product.parameters;
	const problems = parameters.map((p, i) => parameterProblems(p, i, parameters));
	const rules = product.rules ?? [];
	const rulesProblems = rules.map((r, i) => ruleProblems(r, i, product));
	const count = (list: string[][]) => list.filter((p) => p.length > 0).length;
	const dirty = JSON.stringify(product) !== JSON.stringify(saved);

	function setParameters(next: Parameter[]) {
		setProduct((prev) => (prev ? { ...prev, parameters: next } : prev));
	}

	function setRules(next: Rule[]) {
		setProduct((prev) => (prev ? { ...prev, rules: next } : prev));
	}

	function add(type: ParamType) {
		const param = newParameter(type, parameters.map((p) => p.id));
		setParameters([...parameters, param]);
		setSelection(parameters.length);
	}

	function update(index: number, param: Parameter) {
		setParameters(parameters.map((p, i) => (i === index ? param : p)));
	}

	function remove(index: number) {
		const param = parameters[index];
		const usages = usagesOf(product!, param.id);
		let message = `Supprimer le paramètre "${param.label}" ?`;
		if (REQUIRED_IDS.includes(param.id)) message += `\n\nCe paramètre est obligatoire : le produit ne pourra plus être publié sans lui.`;
		if (usages.length > 0) message += `\n\nIl est utilisé par :\n- ${usages.join('\n- ')}\n\nCes éléments ne fonctionneront plus.`;
		if (!window.confirm(message)) return;

		setParameters(parameters.filter((_, i) => i !== index));
		setSelection(parameters.length > 1 ? Math.min(index, parameters.length - 2) : null);
	}

	return (
		<AppLayout title="Configurer" breadcrumb={`Produits / ${product.label} / ${tab === 'rules' ? 'Règles' : 'Paramètres'}`} nav={NAV}>
			<div className={styles.header}>
				<div>
					<h2 className={styles.productTitle}>{product.label}</h2>
					<p className={styles.muted}>{product.manufacturer}</p>
				</div>
				{dirty && <Badge tone="warning">Non enregistré</Badge>}
			</div>

			<div className={styles.tabs} role="tablist" aria-label="Sections du produit">
				{(
					[
						['parameters', `Paramètres (${parameters.length})`, count(problems)],
						['rules', `Règles (${rules.length})`, count(rulesProblems)],
					] as const
				).map(([key, label, invalid]) => (
					<button
						key={key}
						type="button"
						role="tab"
						aria-selected={tab === key}
						className={`${styles.tab}${tab === key ? ` ${styles.tabActive}` : ''}`}
						onClick={() => setTab(key)}
					>
						{label}
						{invalid > 0 && <Badge tone="error">{invalid} à corriger</Badge>}
					</button>
				))}
			</div>

			{tab === 'rules' ? (
				<RulesSection product={product} problems={rulesProblems} onChange={setRules} />
			) : (
				<div className={styles.grid}>
					<div className={styles.card}>
						<div className={styles.listHeader}>
							<h3 className={styles.listTitle}>Paramètres ({parameters.length})</h3>
							<Button variant="primary" onClick={() => setSelection('new')}>
								Ajouter
							</Button>
						</div>

						<ul className={styles.list}>
							{parameters.map((param, index) => (
								<li key={index}>
									<button
										type="button"
										className={`${styles.item}${selection === index ? ` ${styles.selected}` : ''}`}
										aria-current={selection === index ? 'true' : undefined}
										onClick={() => setSelection(index)}
									>
										<span className={styles.itemText}>
											<span className={styles.itemLabel}>{param.label || 'Sans libellé'}</span>
											<span className={styles.itemMeta}>
												{param.id || '?'} · {TYPE_LABELS[param.type]}
											</span>
										</span>
										{problems[index].length > 0 && (
											<Badge tone="error">
												{problems[index].length} à corriger
											</Badge>
										)}
									</button>
								</li>
							))}
						</ul>
					</div>

					<div className={styles.card}>
						{selection === 'new' ? (
							<section aria-labelledby="new-title">
								<h2 id="new-title" className={styles.sheetTitle}>
									Nouveau paramètre : quel type ?
								</h2>
								<div className={styles.types}>
									{TYPES.map((type) => (
										<button key={type} type="button" className={styles.typeCard} onClick={() => add(type)}>
											<span className={styles.typeLabel}>{TYPE_LABELS[type]}</span>
											<span className={styles.muted}>{TYPE_EXAMPLES[type]}</span>
										</button>
									))}
								</div>
								<Button onClick={() => setSelection(null)}>Annuler</Button>
							</section>
						) : selection !== null && parameters[selection] ? (
							<ParameterSheet
								key={selection}
								param={parameters[selection]}
								product={product}
								problems={problems[selection]}
								onChange={(p) => update(selection, p)}
								onDelete={() => remove(selection)}
							/>
						) : (
							<p className={styles.muted}>Sélectionnez un paramètre, ou cliquez sur Ajouter.</p>
						)}
					</div>
				</div>
			)}
		</AppLayout>
	);
}
