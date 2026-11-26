// Contrat entre le front et l'API.
// Les types reprennent src/Engine/Model.cs et src/Engine/Result.cs, en camelCase
// (format JSON par défaut d'ASP.NET).

import productMock from '@/mocks/product-model-v1.json';
import evaluateMocks from '@/mocks/evaluate-results.json';

// ---------- Valeurs ----------

// Une valeur saisie : nombre (largeur), choix (couleur) ou option cochée (volet)
export type Value = number | string | boolean;

// Ce que le vendeur envoie à evaluate : id du paramètre -> valeur
export type Configuration = Record<string, Value>;

// ---------- Produit (src/Engine/Model.cs) ----------

export type ParamType = 'number' | 'choice' | 'option';

export type Price =
	| { type: 'fixed'; amount: number }
	| { type: 'percent'; rate: number }
	| { type: 'perUnit'; rate: number; param: string };

export interface Parameter {
	id: string;
	label: string;
	type: ParamType;
	min?: number;
	max?: number;
	choices?: string[];
	default?: Value;
	price?: Price | null;
}

// Seuls les champs utiles à l'écran Simulation sont typés (pas grid ni rules)
export interface Product {
	id: string;
	label: string;
	manufacturer: string;
	parameters: Parameter[];
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
	total: number | null; // null seulement si un prix manque (hors grille) ou si une saisie est fausse
	version: number;
	priceLines: PriceLine[];
	options: OptionState[];
	errors: string[];
	values: Configuration;
}

// ---------- Appels (simulés tant que l'API n'est pas prête) ----------

export async function getProduct(productId: string): Promise<Product> {
	// TODO API : return (await fetch(`/products/${productId}/draft`)).json();
	void productId;
	return productMock as Product;
}

type Scenario = keyof typeof evaluateMocks;

// Le mock ne calcule rien : il renvoie l'un des 4 résultats de référence produits par le vrai
// moteur (tools/Sandbox). Les autres configurations retombent sur le scénario par défaut.
function pickScenario(config: Configuration): Scenario {
	if (config.largeur === 2400 && config.hauteur === 2100) return 'notManufacturable';
	if (config.motorisation === true && Number(config.dormant ?? 70) < 60) return 'greyMotorThinFrame';
	if (config.motorisation === true) return 'greyMotor';
	return 'defaults';
}

export async function evaluate(productId: string, config: Configuration): Promise<EvaluationResult> {
	// TODO API :
	// const res = await fetch(`/products/${productId}/evaluate`, {
	//   method: 'POST',
	//   headers: { 'Content-Type': 'application/json' },
	//   body: JSON.stringify(config),
	// });
	// return res.json();
	void productId;
	return evaluateMocks[pickScenario(config)] as EvaluationResult;
}
