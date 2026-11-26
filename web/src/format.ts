const euros = new Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'EUR' });

// 368.5 -> "368,50 €" (espace avant le symbole, comme dans la charte)
export function formatEuros(amount: number): string {
	return euros.format(amount);
}
