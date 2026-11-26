import { useEffect, useState } from 'react';
import { API_URL, evaluate, getPublished, listProducts } from '@/api';
import type { Configuration, EvaluationResult, Product, Value } from '@/api';
import { formatEuros, formatOptionPrice } from '@/format';
import Accordion from '@/components/Accordion';
import AppLayout from '@/components/AppLayout';
import Badge from '@/components/Badge';
import Button from '@/components/Button';
import NumberField from '@/components/NumberField';
import OptionItem from '@/components/OptionItem';
import QuotePanel from '@/components/QuotePanel';
import SelectField from '@/components/SelectField';
import { hrefFor, navFor } from '@/nav';
import styles from '@/pages/Simulation.module.css';

const EVALUATION_DELAY_MS = 150;

function errorMessage(e: unknown): string {
	if (e instanceof TypeError) return `Impossible de joindre l'API (${API_URL}).`;
	return e instanceof Error ? e.message : String(e);
}

function initialConfig(product: Product): Configuration {
	const config: Configuration = {};
	for (const param of product.parameters) {
		if (param.default != null) config[param.id] = param.default;
		else if (param.type === 'choice' && param.choices?.length) config[param.id] = param.choices[0];
		else if (param.type === 'option') config[param.id] = false;
	}
	return config;
}

function keepValues(product: Product, previous: Configuration): Configuration {
	const config = initialConfig(product);
	for (const param of product.parameters) {
		const value = previous[param.id];
		const fits =
			(param.type === 'number' && typeof value === 'number') ||
			(param.type === 'option' && typeof value === 'boolean') ||
			(param.type === 'choice' && typeof value === 'string' && (param.choices ?? []).includes(value));
		if (fits) config[param.id] = value;
	}
	return config;
}

export default function Simulation({ productId }: { productId: string }) {
	const [product, setProduct] = useState<Product | null>(null);
	const [latest, setLatest] = useState<number | null>(null);
	const [version, setVersion] = useState<number | null>(null);
	const [productVersion, setProductVersion] = useState<number | null>(null);
	const [config, setConfig] = useState<Configuration>({});
	const [result, setResult] = useState<EvaluationResult | null>(null);
	const [error, setError] = useState<string | null>(null);
	const [notPublished, setNotPublished] = useState(false);
	const NAV = navFor('simulation', productId);

	useEffect(() => {
		listProducts()
			.then((list) => {
				const n = list.find((p) => p.id === productId)?.publishedVersion ?? null;
				if (n === null) return setNotPublished(true);
				setLatest(n);
				setVersion(n);
			})
			.catch((e: unknown) => setError(errorMessage(e)));
	}, [productId]);

	useEffect(() => {
		if (version === null) return;
		let cancelled = false;
		getPublished(productId, version)
			.then((p) => {
				if (cancelled) return;
				setProduct(p);
				setProductVersion(version);
				setConfig((prev) => keepValues(p, prev));
			})
			.catch((e: unknown) => {
				if (!cancelled) setError(errorMessage(e));
			});
		return () => {
			cancelled = true;
		};
	}, [productId, version]);

	useEffect(() => {
		if (!product || productVersion === null) return;
		let cancelled = false;
    
		const timer = window.setTimeout(() => {
      evaluate(productId, config, productVersion)
        .then((r) => {
          if (cancelled) return;
          setResult(r);
          setError(null);
        })
        .catch((e: unknown) => {
          if (!cancelled) setError(errorMessage(e));
        });
		}, EVALUATION_DELAY_MS);

		return () => {
			window.clearTimeout(timer);
			cancelled = true;
		};
	}, [productId, product, productVersion, config]);

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
				{notPublished ? (
					<p className={styles.error} role="alert">
						Ce produit n'est pas encore publié : la simulation utilise la dernière version publiée.{' '}
						<a href={hrefFor('configurer', productId)}>Le configurer et le publier</a>
					</p>
				) : error ? (
					<p className={styles.error} role="alert">
						{error}
					</p>
				) : (
					<p className={styles.loading}>Chargement du produit…</p>
				)}
			</AppLayout>
		);
	}

	const fields = product.parameters.filter((p) => p.type !== 'option');
	const options = product.parameters.filter((p) => p.type === 'option');

	const lineAmount = (label: string) => result?.priceLines.find((l) => l.label === label)?.amount;
	const optionsTotal = options.reduce((sum, o) => sum + (lineAmount(o.label) ?? 0), 0);

	return (
		<AppLayout title="Simulation" breadcrumb={`Produits / ${product.label}`} nav={NAV}>
			{error && (
				<p className={styles.error} role="alert">
					{error}
				</p>
			)}
			<div className={styles.grid}>
				<div className={styles.card}>
					<div className={styles.productHeader}>
						<div>
							<h2 className={styles.productTitle}>{product.label}</h2>
							<p className={styles.manufacturer}>{product.manufacturer}</p>
						</div>
						<div className={styles.headerActions}>
							{latest !== null && version !== null && (
								<SelectField
									id="simulation-version"
									label="Version du produit"
									value={String(version)}
									choices={Array.from({ length: latest }, (_, i) => String(latest - i))}
									labelOf={(v) => (Number(v) === latest ? `v${v} (dernière)` : `v${v}`)}
									onChange={(v) => setVersion(Number(v))}
								/>
							)}
							<Button onClick={() => setConfig(initialConfig(product))}>Réinitialiser</Button>
						</div>
					</div>
					{version !== null && latest !== null && version < latest && (
						<p className={styles.oldVersion}>
							<Badge tone="warning">Ancienne version</Badge> Vous simulez la v{version}. La dernière version publiée est la v{latest}.
						</p>
					)}

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
										min={param.min ?? undefined}
										max={param.max ?? undefined}
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
								const sourceId = state?.autoAddedBy;
								const sourceLabel = result?.options.find((o) => o.id === sourceId)?.label;
								const amount = lineAmount(param.label);
								let priceLabel = formatOptionPrice(param.price, product.parameters);
								if (param.price && state?.selected && amount !== undefined) {
									priceLabel = formatEuros(amount);
								}
								return (
									<OptionItem
										key={param.id}
										id={`param-${param.id}`}
										label={param.label}
										checked={state?.selected ?? config[param.id] === true}
										available={state?.available ?? true}
										autoAdded={state?.autoAdded ?? false}
										autoAddedByLabel={sourceLabel}
										reasons={state?.reasons ?? []}
										amount={priceLabel}
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
