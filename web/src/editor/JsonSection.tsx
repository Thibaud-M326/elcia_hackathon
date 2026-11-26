import { useEffect, useRef, useState } from 'react';
import type { Product } from '@/api';
import Button from '@/components/Button';
import { parseProduct, toJson } from '@/editor/json';
import styles from '@/editor/Json.module.css';

interface JsonSectionProps {
	product: Product;
	onApply: (product: Product) => void;
	// Prévient l'écran quand du texte modifié n'est pas encore appliqué (confirmation en quittant)
	onPendingChange: (pending: boolean) => void;
}

// Le brouillon complet en JSON : sert surtout pour la grille de prix, qui n'a pas de formulaire
export default function JsonSection({ product, onApply, onPendingChange }: JsonSectionProps) {
	const current = toJson(product);
	const [text, setText] = useState(current);
	const [errors, setErrors] = useState<string[]>([]);
	const pending = text !== current;
	const gutterRef = useRef<HTMLDivElement>(null);
	const lineCount = text.split('\n').length;
	// Le message de JSON.parse donne la ligne de l'erreur : "(line 3 column 11)"
	const errorLine = Number(errors[0]?.match(/line (\d+)/)?.[1] ?? 0);

	useEffect(() => {
		onPendingChange(pending);
	}, [pending, onPendingChange]);

	useEffect(() => () => onPendingChange(false), [onPendingChange]);

	function apply() {
		const result = parseProduct(text, product.id);
		if (result.errors) return setErrors(result.errors);
		setErrors([]);
		setText(toJson(result.product));
		onApply(result.product);
	}

	function reload() {
		setText(current);
		setErrors([]);
	}

	return (
		<section className={styles.section} aria-labelledby="json-title">
			<div className={styles.header}>
				<div>
					<h3 id="json-title" className={styles.title}>
						Brouillon complet en JSON
					</h3>
					<p className={styles.muted}>Pour la grille de prix et les cas sans formulaire. Appliquer, puis Enregistrer ou Publier.</p>
				</div>
				<div className={styles.buttons}>
					{pending && <span className={styles.pending}>Modifié, pas encore appliqué</span>}
					<Button onClick={reload} disabled={!pending}>
						Recharger
					</Button>
					<Button variant="primary" onClick={apply} disabled={!pending}>
						Appliquer
					</Button>
				</div>
			</div>

			{errors.length > 0 && (
				<div className={styles.errors} role="alert">
					<strong>Le JSON n'a pas été appliqué :</strong>
					<ul>
						{errors.map((e) => (
							<li key={e}>{e}</li>
						))}
					</ul>
				</div>
			)}

			<div className={styles.editor}>
				{/* Numéros de ligne : défilent avec le texte (scrollTop recopié) */}
				<div ref={gutterRef} className={styles.gutter} aria-hidden="true">
					{Array.from({ length: lineCount }, (_, i) => (
						<div key={i} className={i + 1 === errorLine ? styles.errorLine : undefined}>
							{i + 1}
						</div>
					))}
				</div>
				<textarea
					aria-labelledby="json-title"
					className={styles.text}
					value={text}
					wrap="off"
					spellCheck={false}
					autoCapitalize="off"
					autoCorrect="off"
					onChange={(e) => {
						setText(e.target.value);
						// Les erreurs portaient sur l'ancien texte : on les retire dès qu'il change
						setErrors([]);
					}}
					onScroll={(e) => {
						if (gutterRef.current) gutterRef.current.scrollTop = e.currentTarget.scrollTop;
					}}
				/>
			</div>
		</section>
	);
}
