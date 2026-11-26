import { useEffect, useState } from 'react';
import { API_URL, listProducts } from '@/api';
import type { ProductSummary } from '@/api';
import AppLayout from '@/components/AppLayout';
import Badge from '@/components/Badge';
import Button from '@/components/Button';
import CreateProductForm from '@/pages/CreateProductForm';
import { hrefFor, navFor } from '@/nav';
import styles from '@/pages/Products.module.css';

const NAV = navFor('produits');

const go = (href: string) => {
	window.location.hash = href;
};

export default function Products() {
	const [products, setProducts] = useState<ProductSummary[] | null>(null);
	const [error, setError] = useState<string | null>(null);
	const [creating, setCreating] = useState(false);

	useEffect(() => {
		listProducts()
			.then(setProducts)
			.catch((e: unknown) => setError(e instanceof TypeError ? `Impossible de joindre l'API (${API_URL}). Lancez : dotnet run --project src/Api` : String(e instanceof Error ? e.message : e)));
	}, []);

	return (
		<AppLayout title="Produits" nav={NAV}>
			<div className={styles.page}>
				{error && (
					<p className={styles.loadError} role="alert">
						{error}
					</p>
				)}

				{!error && products === null && <p className={styles.muted}>Chargement des produits...</p>}

				{products !== null && (
					<>
						<div className={styles.header}>
							<h2 className={styles.title}>Produits ({products.length})</h2>
							{!creating && (
								<Button variant="primary" onClick={() => setCreating(true)}>
									Nouveau produit
								</Button>
							)}
						</div>

						{creating && <CreateProductForm products={products} onCreated={(id) => go(hrefFor('configurer', id))} onCancel={() => setCreating(false)} />}

						{products.length === 0 ? (
							<p className={styles.empty}>Aucun produit pour l'instant. Créez-en un avec le bouton Nouveau produit.</p>
						) : (
							<ul className={styles.list}>
								{products.map((p) => (
									<li key={p.id} className={styles.row}>
										<div className={styles.rowText}>
											<span className={styles.label}>{p.label}</span>
											<span className={styles.meta}>
												{p.manufacturer || 'Fabricant non renseigné'} · {p.id}
											</span>
										</div>
										{p.publishedVersion != null ? <Badge tone="success">v{p.publishedVersion}</Badge> : <Badge tone="warning">Brouillon</Badge>}
										<div className={styles.rowButtons}>
											<Button
												onClick={() => go(hrefFor('simulation', p.id))}
												disabled={p.publishedVersion == null}
												title={p.publishedVersion == null ? "Publiez d'abord le produit depuis Configurer" : undefined}
											>
												Simuler
											</Button>
											<Button variant="primary" onClick={() => go(hrefFor('configurer', p.id))}>
												Configurer
											</Button>
										</div>
									</li>
								))}
							</ul>
						)}
					</>
				)}
			</div>
		</AppLayout>
	);
}
