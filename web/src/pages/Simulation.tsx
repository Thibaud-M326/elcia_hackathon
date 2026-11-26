import { useEffect, useState } from 'react';
import { evaluate, getProduct } from '@/api';
import type { Configuration, EvaluationResult, Product, Value } from '@/api';
import { formatEuros } from '@/format';
import Accordion from '@/components/Accordion';
import AppLayout from '@/components/AppLayout';
import Button from '@/components/Button';
import NumberField from '@/components/NumberField';
import OptionItem from '@/components/OptionItem';
import QuotePanel from '@/components/QuotePanel';
import SelectField from '@/components/SelectField';
import styles from '@/pages/Simulation.module.css';

const NAV = [{ label: 'Simulation', href: '#simulation', active: true }];
const EVALUATION_DELAY_MS = 150;

function initialConfig(product: Product): Configuration {
	const config: Configuration = {};
	for (const param of product.parameters) {
		if (param.default !== undefined) config[param.id] = param.default;
		else if (param.type === 'choice' && param.choices?.length) config[param.id] = param.choices[0];
		else if (param.type === 'option') config[param.id] = false;
	}
	return config;
}

export default function Simulation({ productId }: { productId: string }) {
	const [product, setProduct] = useState<Product | null>(null);
	const [config, setConfig] = useState<Configuration>({});
	const [result, setResult] = useState<EvaluationResult | null>(null);

	useEffect(() => {
		getProduct(productId).then((p) => {
			setProduct(p);
			setConfig(initialConfig(p));
		});
	}, [productId]);

	useEffect(() => {
		if (!product) return;
		let cancelled = false;
		const timer = window.setTimeout(() => {
			evaluate(productId, config).then((r) => {
				if (!cancelled) setResult(r);
			});
		}, EVALUATION_DELAY_MS);

		return () => {
			window.clearTimeout(timer);
			cancelled = true;
		};
	}, [productId, product, config]);

	function setValue(id: string, value: Value | undefined) {
		setConfig((prev) => {
			const next = { ...prev };
			if (value === undefined) delete next[id];
			else next[id] = value;
			return next;
		});
	}

	if (!product) {
		return (
			<AppLayout title="Simulation" nav={NAV}>
				<p className={styles.loading}>Chargement du produit…</p>
			</AppLayout>
		);
	}

	const fields = product.parameters.filter((p) => p.type !== 'option');
	const options = product.parameters.filter((p) => p.type === 'option');

	const lineAmount = (label: string) => result?.priceLines.find((l) => l.label === label)?.amount;
	const optionsTotal = options.reduce((sum, o) => sum + (lineAmount(o.label) ?? 0), 0);

	return (
		<AppLayout title="Simulation" breadcrumb={`Produits / ${product.label}`} nav={NAV}>
			<div className={styles.grid}>
				<div className={styles.card}>
					<div className={styles.productHeader}>
						<div>
							<h2 className={styles.productTitle}>{product.label}</h2>
							<p className={styles.manufacturer}>{product.manufacturer}</p>
						</div>
						<Button onClick={() => setConfig(initialConfig(product))}>Réinitialiser</Button>
					</div>

					<Accordion title="Dimensions et finition" defaultOpen>
						<div className={styles.fields}>
							{fields.map((param) =>
								param.type === 'choice' ? (
									<SelectField
										key={param.id}
										id={`param-${param.id}`}
										label={param.label}
										value={String(config[param.id] ?? '')}
										choices={param.choices ?? []}
										onChange={(v) => setValue(param.id, v)}
									/>
								) : (
									<NumberField
										key={param.id}
										id={`param-${param.id}`}
										label={param.label}
										value={typeof config[param.id] === 'number' ? (config[param.id] as number) : undefined}
										min={param.min}
										max={param.max}
										onChange={(v) => setValue(param.id, v)}
									/>
								),
							)}
						</div>
					</Accordion>

					<Accordion title="Options" amount={formatEuros(optionsTotal)} muted={optionsTotal === 0} defaultOpen>
						<ul className={styles.options}>
							{options.map((param) => {
								const state = result?.options.find((o) => o.id === param.id);
								const amount = lineAmount(param.label);
								return (
									<OptionItem
										key={param.id}
										id={`param-${param.id}`}
										label={param.label}
										checked={state?.selected ?? config[param.id] === true}
										available={state?.available ?? true}
										autoAdded={state?.autoAdded ?? false}
										reasons={state?.reasons ?? []}
										amount={amount !== undefined ? formatEuros(amount) : undefined}
										onChange={(checked) => setValue(param.id, checked)}
									/>
								);
							})}
						</ul>
					</Accordion>
				</div>

				<div className={styles.side}>
					<QuotePanel productLabel={product.label} result={result} />
				</div>
			</div>
		</AppLayout>
	);
}
