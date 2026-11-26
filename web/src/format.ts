import type { Parameter, Price } from '@/api';

const euros = new Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'EUR' });
const tariffNumber = new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 20 });

// 368.5 -> "368,50 €" (espace avant le symbole, comme dans la charte)
export function formatEuros(amount: number): string {
	return euros.format(amount);
}

export function formatOptionPrice(price: Price | null | undefined, parameters: Parameter[]): string | undefined {
	if (!price) return undefined;

	if (price.type === 'fixed') {
		return `${tariffNumber.format(price.amount)} €`;
	}
	if (price.type === 'percent') {
		return `${tariffNumber.format(price.rate)} % du prix de base`;
	}

	const pricedParameter = parameters.find((parameter) => parameter.id === price.param);
	if (!pricedParameter) return undefined;

	const rate = tariffNumber.format(price.rate);
	const label = pricedParameter.label;
	const millimetreSuffix = ' (mm)';
	if (label.endsWith(millimetreSuffix)) {
		const name = label.slice(0, -millimetreSuffix.length);
		return `${rate} €/mm × ${name}`;
	}
	return `${rate} € × ${label}`;
}
