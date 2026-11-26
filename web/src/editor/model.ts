// Logique de l'éditeur de produit, sans React : facile à relire et à tester.
// Les contrôles reprennent ceux de src/Engine/Validator.cs, qui reste la référence à la publication.

import type { ParamType, Parameter, Price, PriceType, Product } from '@/api';
import { formatEuros } from '@/format';

export const TYPE_LABELS: Record<ParamType, string> = {
	number: 'Nombre',
	choice: 'Liste de choix',
	option: 'Option',
};

export const TYPE_EXAMPLES: Record<ParamType, string> = {
	number: 'Une valeur saisie entre un minimum et un maximum. Ex : la largeur en mm.',
	choice: 'Une valeur parmi une liste. Ex : la couleur.',
	option: 'Une case à cocher, avec son prix. Ex : le volet roulant.',
};

export const PRICE_LABELS: Record<PriceType | 'none', string> = {
	none: 'Gratuite',
	fixed: 'Montant fixe',
	percent: 'Pourcentage du prix de base',
	perUnit: 'Prix par unité',
};

export const REQUIRED_IDS = ['largeur', 'hauteur'];

export function uniqueId(base: string, taken: string[]): string {
	let id = base;
	for (let n = 2; taken.includes(id); n++) id = `${base}-${n}`;
	return id;
}

export function newParameter(type: ParamType, taken: string[]): Parameter {
	switch (type) {
		case 'number':
			return { id: uniqueId('nouveau-nombre', taken), label: 'Nouveau nombre', type, min: 0, max: 1000, default: 0, choices: null, price: null };
		case 'choice':
			return { id: uniqueId('nouvelle-liste', taken), label: 'Nouvelle liste', type, min: null, max: null, choices: ['choix-1'], default: 'choix-1', price: null };
		case 'option':
			return {
				id: uniqueId('nouvelle-option', taken),
				label: 'Nouvelle option',
				type,
				min: null,
				max: null,
				choices: null,
				default: false,
				price: { type: 'fixed', amount: 0, rate: 0, param: null },
			};
	}
}

export function numericParameters(parameters: Parameter[]): Parameter[] {
	return parameters.filter((p) => p.type === 'number');
}

export function parameterProblems(param: Parameter, index: number, all: Parameter[]): string[] {
	const problems: string[] = [];

	if (!param.label.trim()) problems.push('Le libellé est obligatoire.');

	if (!param.id.trim()) problems.push("L'identifiant est obligatoire.");
	else if (/[\s.]/.test(param.id)) problems.push("L'identifiant ne peut contenir ni espace ni point.");
	else if (all.some((other, i) => i !== index && other.id === param.id)) problems.push(`L'identifiant '${param.id}' est déjà utilisé.`);

	if (param.type === 'number') {
		const { min, max } = param;
		const def = typeof param.default === 'number' ? param.default : null;
		if (min != null && max != null && min > max) problems.push('Le minimum est supérieur au maximum.');
		if (def != null && ((min != null && def < min) || (max != null && def > max))) problems.push('La valeur par défaut est hors des bornes.');
	}

	if (param.type === 'choice') {
		const choices = param.choices ?? [];
		if (choices.length === 0) problems.push('Il faut au moins un choix.');
		if (choices.some((c) => !c.trim())) problems.push('Un choix est vide.');
		if (new Set(choices).size < choices.length) problems.push('La liste contient des choix en double.');
		if (param.default != null && !choices.includes(String(param.default))) problems.push('Le choix par défaut ne fait pas partie de la liste.');
	}

	if (param.type === 'option' && param.price) problems.push(...priceProblems(param.price, all));

	return problems;
}

export function priceProblems(price: Price, all: Parameter[]): string[] {
	switch (price.type) {
		case 'fixed':
			return price.amount < 0 ? ['Le montant doit être positif ou nul.'] : [];
		case 'percent':
			return price.rate < 0 || price.rate > 100 ? ['Le taux doit être compris entre 0 et 100.'] : [];
		case 'perUnit': {
			if (!price.param) return ['Choisissez le nombre qui multiplie le prix.'];
			const target = all.find((p) => p.id === price.param);
			if (!target) return [`Le paramètre '${price.param}' est introuvable.`];
			if (target.type !== 'number') return [`Le paramètre '${price.param}' doit être un nombre.`];
			return [];
		}
	}
}

export function usagesOf(product: Product, id: string): string[] {
	const usages: string[] = [];

	for (const rule of product.rules ?? []) {
		const used =
			rule.option === id || rule.other === id || rule.when.some((c) => c.param === id) || rule.price?.param === id;
		if (used) usages.push(`Règle '${rule.id}'`);
	}

	for (const param of product.parameters) {
		if (param.id !== id && param.price?.type === 'perUnit' && param.price.param === id) usages.push(`Prix de l'option '${param.label}'`);
	}

	return usages;
}

const numbers = new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 4 });

export function describePrice(price: Price | null | undefined, all: Parameter[]): string {
	if (!price) return 'Gratuite';
	switch (price.type) {
		case 'fixed':
			return formatEuros(price.amount);
		case 'percent':
			return `${numbers.format(price.rate)} % du prix de base`;
		case 'perUnit': {
			const target = all.find((p) => p.id === price.param);
			return `${formatEuros(price.rate)} x ${target?.label ?? '?'}`;
		}
	}
}
