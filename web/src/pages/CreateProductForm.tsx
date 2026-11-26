import { useState } from 'react';
import type { FormEvent } from 'react';
import { ApiError, API_URL, getDraft, getPublished, listProducts, saveDraft } from '@/api';
import type { Product, ProductSummary } from '@/api';
import Button from '@/components/Button';
import SelectField from '@/components/SelectField';
import TextField from '@/components/TextField';
import { blankProduct, copyProduct, productIdProblem, slugify } from '@/editor/newProduct';
import styles from '@/pages/Products.module.css';

const BLANK = '';

interface CreateProductFormProps {
	products: ProductSummary[];
	onCreated: (id: string) => void;
	onCancel: () => void;
}

async function loadSource(id: string): Promise<Product> {
	try {
		return await getDraft(id);
	} catch (e) {
		if (e instanceof ApiError && e.status === 404) return getPublished(id);
		throw e;
	}
}

export default function CreateProductForm({ products, onCreated, onCancel }: CreateProductFormProps) {
	const [source, setSource] = useState(products[0]?.id ?? BLANK);
	const [label, setLabel] = useState('');
	const [manufacturer, setManufacturer] = useState(products[0]?.manufacturer ?? '');
	const [id, setId] = useState('');
	const [idTouched, setIdTouched] = useState(false);
	const [busy, setBusy] = useState(false);
	const [error, setError] = useState<string | null>(null);

	const taken = products.map((p) => p.id);
	const idProblem = productIdProblem(id, taken);
	const labelProblem = label.trim() ? null : 'Le nom est obligatoire.';

	function changeSource(next: string) {
		setSource(next);
		const summary = products.find((p) => p.id === next);
		if (summary) setManufacturer(summary.manufacturer);
	}

	function changeLabel(next: string) {
		setLabel(next);
		if (!idTouched) setId(slugify(next));
	}

	async function submit(e: FormEvent) {
		e.preventDefault();
		if (idProblem || labelProblem) return;
		setBusy(true);
		setError(null);
		try {
			const fresh = await listProducts();
			const problem = productIdProblem(id, fresh.map((p) => p.id));
			if (problem) throw new Error(problem);

			const product =
				source === BLANK
					? blankProduct(id, label.trim(), manufacturer.trim())
					: copyProduct(await loadSource(source), id, label.trim(), manufacturer.trim());
			await saveDraft(product);
			onCreated(id);
		} catch (err) {
			setError(err instanceof TypeError ? `Impossible de joindre l'API (${API_URL}).` : err instanceof Error ? err.message : String(err));
			setBusy(false);
		}
	}

	return (
		<form className={styles.form} onSubmit={submit} aria-labelledby="create-title">
			<h3 id="create-title" className={styles.formTitle}>
				Nouveau produit
			</h3>

			<SelectField
				id="create-source"
				label="Partir de"
				value={source}
				choices={[...products.map((p) => p.id), BLANK]}
				labelOf={(value) => (value === BLANK ? 'Produit vierge (largeur, hauteur et une grille à adapter)' : `Copie de : ${products.find((p) => p.id === value)?.label ?? value}`)}
				onChange={changeSource}
			/>

			<div className={styles.formFields}>
				<TextField id="create-label" label="Nom du produit" value={label} error={label ? labelProblem : null} onChange={changeLabel} />
				<TextField id="create-manufacturer" label="Fabricant" value={manufacturer} onChange={setManufacturer} />
				<TextField
					id="create-id"
					label="Identifiant"
					value={id}
					hint="Utilisé dans l'adresse et le nom du dossier. Ne peut plus changer ensuite."
					error={id ? idProblem : null}
					onChange={(value) => {
						setId(value);
						setIdTouched(true);
					}}
				/>
			</div>

			{error && (
				<p className={styles.formError} role="alert">
					{error}
				</p>
			)}

			<div className={styles.formButtons}>
				<Button onClick={onCancel} disabled={busy}>
					Annuler
				</Button>
				<Button type="submit" variant="primary" disabled={busy || idProblem !== null || labelProblem !== null}>
					{busy ? 'Création...' : 'Créer et configurer'}
				</Button>
			</div>
		</form>
	);
}
