// Contrat entre le front et l'API.
// Les types reprennent src/Engine/Model.cs et src/Engine/Result.cs, en camelCase
// (format JSON par défaut d'ASP.NET).

// ---------- Valeurs ----------

// Une valeur saisie : nombre (largeur), choix (couleur) ou option cochée (volet)
export type Value = number | string | boolean;

// Ce que le vendeur envoie à evaluate : id du paramètre -> valeur
export type Configuration = Record<string, Value>;

// ---------- Produit (src/Engine/Model.cs) ----------
// L'API envoie null pour un champ vide (min d'un choix, prix d'un nombre...).

export type ParamType = 'number' | 'choice' | 'option';
export type PriceType = 'fixed' | 'percent' | 'perUnit';
export type RuleType = 'availableIf' | 'incompatible' | 'implies' | 'surcharge';
export type Op = 'eq' | 'neq' | 'gte' | 'lte' | 'in';

// fixed : amount ; percent : rate % du prix de base ; perUnit : rate x valeur du paramètre param
export interface Price {
	type: PriceType;
	amount: number;
	rate: number;
	param: string | null;
}

export interface Parameter {
	id: string;
	label: string;
	type: ParamType;
	min?: number | null;
	max?: number | null;
	choices?: string[] | null;
	default?: Value | null;
	price?: Price | null;
}

export interface Condition {
	param: string;
	op: Op;
	value: Value | Value[];
}

export interface Rule {
	id: string;
	type: RuleType;
	option?: string | null;
	other?: string | null;
	when: Condition[];
	message?: string | null;
	label?: string | null;
	price?: Price | null;
}

export interface Grid {
	label: string;
	widths: number[];
	heights: number[];
	prices: (number | null)[][];
}

export interface Product {
	id: string;
	label: string;
	manufacturer: string;
	parameters: Parameter[];
	grid?: Grid;
	rules?: Rule[];
}

// ---------- Résultat d'un chiffrage (src/Engine/Result.cs) ----------

export interface PriceLine {
	label: string;
	amount: number;
}

export interface OptionState {
	id: string;
	label: string;
	selected: boolean;
	available: boolean;
	autoAdded: boolean;
	reasons: string[];
}

export interface EvaluationResult {
	valid: boolean;
	total: number | null;
	version: number;
	priceLines: PriceLine[];
	options: OptionState[];
	errors: string[];
	values: Configuration;
}


export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5080';

export interface ProductSummary {
	id: string;
	label: string;
	manufacturer: string;
	publishedVersion: number | null;
}

const productPath = (productId: string) => `/products/${encodeURIComponent(productId)}`;
const versionQuery = (version?: number) => (version === undefined ? '' : `?version=${version}`);

async function request<T>(path: string, init?: RequestInit): Promise<T> {
	const res = await fetch(`${API_URL}${path}`, init);
	if (!res.ok) {
		const body = await res.json().catch(() => null);
		const detail = Array.isArray(body?.errors) ? ` : ${body.errors.join(' ')}` : '';
		throw new Error(`HTTP ${res.status} sur ${path}${detail}`);
	}
	return res.json();
}

export function listProducts(): Promise<ProductSummary[]> {
	return request('/products');
}

export function getDraft(productId: string): Promise<Product> {
	return request(`${productPath(productId)}/draft`);
}

export function getPublished(productId: string, version?: number): Promise<Product> {
	return request(`${productPath(productId)}/published${versionQuery(version)}`);
}

export function evaluate(productId: string, config: Configuration, version?: number): Promise<EvaluationResult> {
	return request(`${productPath(productId)}/evaluate${versionQuery(version)}`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify(config),
	});
}
