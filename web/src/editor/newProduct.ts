// Création d'un produit : identifiant, produit vierge publiable, copie d'un produit existant.

import type { Product } from '@/api';

export function slugify(label: string): string {
	return label
		.normalize('NFD')
		.replace(/[̀-ͯ]/g, '')
		.toLowerCase()
		.replace(/[^a-z0-9]+/g, '-')
		.replace(/^-+|-+$/g, '');
}

export function productIdProblem(id: string, taken: string[]): string | null {
	if (!id) return "L'identifiant est obligatoire.";
	if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(id)) return 'Lettres minuscules, chiffres et tirets seulement (ex : porte-alu).';
	if (taken.includes(id)) return `L'identifiant '${id}' est déjà utilisé.`;
	return null;
}

export function blankProduct(id: string, label: string, manufacturer: string): Product {
	return {
		id,
		label,
		manufacturer,
		parameters: [
			{ id: 'largeur', label: 'Largeur (mm)', type: 'number', min: 600, max: 2400, default: 1200, choices: null, price: null },
			{ id: 'hauteur', label: 'Hauteur (mm)', type: 'number', min: 600, max: 2100, default: 1200, choices: null, price: null },
		],
		grid: {
			label: 'Prix de base',
			widths: [600, 1200, 1800, 2400],
			heights: [600, 1200, 2100],
			prices: [
				[100, 150, 200, 250],
				[150, 200, 250, 300],
				[200, 250, 300, 350],
			],
		},
		rules: [],
	};
}

export function copyProduct(source: Product, id: string, label: string, manufacturer: string): Product {
	return { ...structuredClone(source), id, label, manufacturer };
}
