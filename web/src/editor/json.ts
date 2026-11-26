// Onglet JSON : le brouillon complet en texte, et la vérification avant de l'appliquer.
// On ne vérifie ici que ce qui ferait planter l'éditeur ou écraserait un autre produit ;
// le reste (grille, contenu des règles) est vérifié par le serveur à la publication.

import type { Product } from '@/api';

const PARAM_TYPES = ['number', 'choice', 'option'];
const RULE_TYPES = ['availableIf', 'incompatible', 'implies', 'surcharge'];

export type ParseResult = { product: Product; errors?: undefined } | { product?: undefined; errors: string[] };

export function toJson(product: Product): string {
	return JSON.stringify(product, null, 2);
}

const isObject = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);

export function parseProduct(text: string, expectedId: string): ParseResult {
	let data: unknown;
	try {
		data = JSON.parse(text);
	} catch (e) {
		return { errors: [`JSON invalide : ${e instanceof Error ? e.message : String(e)}`] };
	}

	if (!isObject(data)) return { errors: ['Le JSON doit être un objet { ... } qui décrit le produit.'] };

	const errors: string[] = [];

	if (typeof data.id !== 'string' || !data.id) errors.push('Le champ "id" est obligatoire.');
	else if (data.id !== expectedId) errors.push(`L'identifiant ne peut pas changer : "${expectedId}" attendu, "${data.id}" trouvé.`);

	if (typeof data.label !== 'string') errors.push('Le champ "label" doit être du texte.');

	if (!Array.isArray(data.parameters)) errors.push('Le champ "parameters" doit être une liste [ ... ].');
	else
		data.parameters.forEach((p, i) => {
			const where = `Paramètre ${i + 1}`;
			if (!isObject(p)) return errors.push(`${where} : doit être un objet { ... }.`);
			if (typeof p.id !== 'string') errors.push(`${where} : "id" doit être du texte.`);
			if (typeof p.label !== 'string') errors.push(`${where} : "label" doit être du texte.`);
			if (!PARAM_TYPES.includes(p.type as string)) errors.push(`${where} : "type" doit valoir number, choice ou option.`);
		});

	if (data.rules != null && !Array.isArray(data.rules)) errors.push('Le champ "rules" doit être une liste [ ... ].');
	else if (Array.isArray(data.rules))
		data.rules.forEach((r, i) => {
			const where = `Règle ${i + 1}`;
			if (!isObject(r)) return errors.push(`${where} : doit être un objet { ... }.`);
			if (typeof r.id !== 'string') errors.push(`${where} : "id" doit être du texte.`);
			if (!RULE_TYPES.includes(r.type as string)) errors.push(`${where} : "type" doit valoir availableIf, incompatible, implies ou surcharge.`);
			if (r.when != null && !Array.isArray(r.when)) errors.push(`${where} : "when" doit être une liste [ ... ].`);
			else if (Array.isArray(r.when) && r.when.some((c) => !isObject(c) || typeof c.param !== 'string'))
				errors.push(`${where} : chaque condition doit être un objet avec un "param" en texte.`);
		});

	if (errors.length > 0) return { errors };

	// "when" peut être omis dans le JSON (comme côté C#) : l'éditeur attend une liste
	const product = data as unknown as Product;
	return {
		product: {
			...product,
			manufacturer: typeof product.manufacturer === 'string' ? product.manufacturer : '',
			rules: product.rules?.map((r) => ({ ...r, when: r.when ?? [] })),
		},
	};
}
