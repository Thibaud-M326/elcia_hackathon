// Logique de l'éditeur de règles, sans React (même principe que model.ts).
// Les contrôles reprennent ceux de src/Engine/Validator.cs pour les règles.

import type { Condition, Op, Parameter, Product, Rule, RuleType, Value } from '@/api';
import { describePrice, priceProblems, uniqueId } from '@/editor/model';

export const RULE_TYPES: RuleType[] = ['availableIf', 'incompatible', 'implies', 'surcharge'];

export const RULE_TYPE_LABELS: Record<RuleType, string> = {
	availableIf: 'Disponibilité',
	incompatible: 'Incompatibilité',
	implies: 'Implication',
	surcharge: 'Plus-value',
};

export const RULE_TYPE_EXAMPLES: Record<RuleType, string> = {
	availableIf: "Volet roulant n'est possible que si le dormant est au moins 60",
	incompatible: 'Croisillons est incompatible avec Vitrage feuilleté',
	implies: 'Motorisation entraîne Volet roulant',
	surcharge: "Si la couleur n'est pas blanc, ajouter 12 % du prix de base",
};

export const OP_LABELS: Record<Op, string> = {
	eq: 'est',
	neq: "n'est pas",
	gte: 'est au moins',
	lte: 'est au plus',
	in: "est l'un de",
};

const ID_BASES: Record<RuleType, string> = {
	availableIf: 'disponibilite',
	incompatible: 'incompatibilite',
	implies: 'implication',
	surcharge: 'plus-value',
};

export function optionParameters(product: Product): Parameter[] {
	return product.parameters.filter((p) => p.type === 'option');
}

export function opsFor(param: Parameter | undefined): Op[] {
	switch (param?.type) {
		case 'number':
			return ['gte', 'lte', 'eq', 'neq'];
		case 'choice':
			return ['eq', 'neq', 'in'];
		case 'option':
			return ['eq'];
		default:
			return ['eq'];
	}
}

export function defaultValue(param: Parameter | undefined, op: Op): Value | Value[] {
	if (!param) return '';
	switch (param.type) {
		case 'number':
			return typeof param.default === 'number' ? param.default : (param.min ?? 0);
		case 'choice': {
			const first = param.choices?.[0] ?? '';
			return op === 'in' ? [first] : first;
		}
		case 'option':
			return true;
	}
}

export function withParam(condition: Condition, param: Parameter | undefined): Condition {
	const ops = opsFor(param);
	const op = ops.includes(condition.op) ? condition.op : ops[0];
	return { param: param?.id ?? '', op, value: defaultValue(param, op) };
}

export function withOp(condition: Condition, op: Op, param: Parameter | undefined): Condition {
	const wasList = Array.isArray(condition.value);
	if (op === 'in' && !wasList) return { ...condition, op, value: condition.value === '' ? defaultValue(param, op) : [condition.value as Value] };
	if (op !== 'in' && wasList) {
		const first = (condition.value as Value[])[0];
		return { ...condition, op, value: first ?? defaultValue(param, op) };
	}
	return { ...condition, op };
}

export function newCondition(product: Product): Condition {
	const param = product.parameters.find((p) => p.type === 'number') ?? product.parameters[0];
	return withParam({ param: '', op: 'gte', value: '' }, param);
}

export function newRule(type: RuleType, product: Product): Rule {
	const options = optionParameters(product).map((p) => p.id);
	const id = uniqueId(ID_BASES[type], (product.rules ?? []).map((r) => r.id));
	const base: Rule = { id, type, option: null, other: null, when: [], message: null, label: null, price: null };

	switch (type) {
		case 'availableIf':
			return { ...base, option: options[0] ?? null, when: [newCondition(product)], message: '' };
		case 'incompatible':
			return { ...base, option: options[0] ?? null, other: options[1] ?? null, message: '' };
		case 'implies':
			return { ...base, option: options[0] ?? null, other: options[1] ?? null };
		case 'surcharge':
			return { ...base, when: [newCondition(product)], label: '', price: { type: 'fixed', amount: 0, rate: 0, param: null } };
	}
}

export function duplicateRule(rule: Rule, rules: Rule[]): Rule {
	return { ...structuredClone(rule), id: uniqueId(rule.id, rules.map((r) => r.id)) };
}

function optionProblem(id: string | null | undefined, what: string, product: Product): string | null {
	if (!id) return `Choisissez ${what}.`;
	const param = product.parameters.find((p) => p.id === id);
	if (!param) return `L'option '${id}' est introuvable.`;
	if (param.type !== 'option') return `'${id}' n'est pas une option.`;
	return null;
}

function conditionProblems(c: Condition, n: number, product: Product): string[] {
	const where = `Condition ${n}`;
	const param = product.parameters.find((p) => p.id === c.param);
	if (!c.param) return [`${where} : choisissez un paramètre.`];
	if (!param) return [`${where} : le paramètre '${c.param}' est introuvable.`];

	const problems: string[] = [];
	if ((c.op === 'gte' || c.op === 'lte') && param.type !== 'number') problems.push(`${where} : "${OP_LABELS[c.op]}" demande un nombre.`);

	const values = Array.isArray(c.value) ? c.value : [c.value];
	if (c.op === 'in' && values.length === 0) problems.push(`${where} : cochez au moins une valeur.`);

	for (const v of values) {
		if (param.type === 'number' && typeof v !== 'number') problems.push(`${where} : la valeur doit être un nombre.`);
		if (param.type === 'choice' && !(param.choices ?? []).includes(String(v))) problems.push(`${where} : '${v}' ne fait pas partie des choix de ${param.label}.`);
		if (param.type === 'option' && typeof v !== 'boolean') problems.push(`${where} : la valeur doit être oui ou non.`);
	}
	return problems;
}

export function ruleProblems(rule: Rule, index: number, product: Product): string[] {
	const problems: string[] = [];
	const rules = product.rules ?? [];

	if (!rule.id.trim()) problems.push("L'identifiant est obligatoire.");
	else if (rules.some((other, i) => i !== index && other.id === rule.id)) problems.push(`L'identifiant '${rule.id}' est déjà utilisé.`);

	const needsOther = rule.type === 'incompatible' || rule.type === 'implies';
	const needsConditions = rule.type === 'availableIf' || rule.type === 'surcharge';

	if (rule.type !== 'surcharge') {
		const p = optionProblem(rule.option, "l'option", product);
		if (p) problems.push(p);
	}
	if (needsOther) {
		const p = optionProblem(rule.other, 'la seconde option', product);
		if (p) problems.push(p);
		else if (rule.option === rule.other) problems.push('Une règle ne peut pas porter deux fois sur la même option.');
	}

	if (needsConditions && rule.when.length === 0) problems.push('Ajoutez au moins une condition.');
	rule.when.forEach((c, i) => problems.push(...conditionProblems(c, i + 1, product)));

	if ((rule.type === 'availableIf' || rule.type === 'incompatible') && !rule.message?.trim()) problems.push('Le message est obligatoire.');

	if (rule.type === 'surcharge') {
		if (!rule.label?.trim()) problems.push('Le libellé sur le devis est obligatoire.');
		if (!rule.price) problems.push('Le prix est obligatoire.');
		else problems.push(...priceProblems(rule.price, product.parameters));
	}

	for (const match of (rule.message ?? '').matchAll(/\{([^{}]+)\}/g)) {
		if (!product.parameters.some((p) => p.id === match[1])) problems.push(`Message : {${match[1]}} ne correspond à aucun paramètre.`);
	}

	return problems;
}

function labelOf(id: string | null | undefined, product: Product): string {
	return product.parameters.find((p) => p.id === id)?.label ?? '?';
}

function valueText(v: Value, param: Parameter | undefined): string {
	if (param?.type === 'option') return v === true ? 'cochée' : 'non cochée';
	return String(v);
}

export function describeCondition(c: Condition, product: Product): string {
	const param = product.parameters.find((p) => p.id === c.param);
	const value = Array.isArray(c.value) ? c.value.map((v) => valueText(v, param)).join(', ') : valueText(c.value, param);
	return `${param?.label ?? '?'} ${OP_LABELS[c.op]} ${value}`;
}

export function describeRule(rule: Rule, product: Product): string {
	const conditions = rule.when.map((c) => describeCondition(c, product)).join(' et ') || '...';
	switch (rule.type) {
		case 'availableIf':
			return `${labelOf(rule.option, product)} n'est possible que si ${conditions}`;
		case 'incompatible':
			return `${labelOf(rule.option, product)} est incompatible avec ${labelOf(rule.other, product)}`;
		case 'implies':
			return `${labelOf(rule.option, product)} entraîne ${labelOf(rule.other, product)}`;
		case 'surcharge':
			return `Si ${conditions}, ajouter ${describePrice(rule.price, product.parameters)}`;
	}
}
