import type { EvaluationResult } from '@/api';
import { formatEuros } from '@/format';
import Badge from '@/components/Badge';
import styles from '@/components/QuotePanel.module.css';

interface QuotePanelProps {
	productLabel: string;
	result: EvaluationResult | null;
}

export default function QuotePanel({ productLabel, result }: QuotePanelProps) {
	return (
		<aside className={styles.panel} aria-labelledby="quote-title" aria-live="polite">
			<div className={styles.header}>
				<h2 id="quote-title" className={styles.title}>
					Devis
				</h2>
				{result && <Badge tone={result.valid ? 'success' : 'error'}>{result.valid ? 'Valide' : 'Invalide'}</Badge>}
			</div>
			<p className={styles.product}>{productLabel}</p>

			{!result ? (
				<p className={styles.product}>Calcul…</p>
			) : (
				<>
					<dl className={styles.lines}>
						{result.priceLines.map((line, i) => (
							<div key={`${line.label}-${i}`} className={styles.line}>
								<dt>{line.label}</dt>
								<dd>{formatEuros(line.amount)}</dd>
							</div>
						))}
					</dl>

					<div className={styles.rule} />

					<div className={styles.total}>
						<span>Total HT</span>
						<span>{result.total === null ? '-' : formatEuros(result.total)}</span>
					</div>

					{result.errors.length > 0 && (
						<ul className={styles.errors}>
							{result.errors.map((error) => (
								<li key={error}>{error}</li>
							))}
						</ul>
					)}

					<p className={styles.version}>Version {result.version}</p>
				</>
			)}
		</aside>
	);
}
