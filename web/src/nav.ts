// Navigation entre les écrans par le hash de l'URL (#simulation, #configurer),
// sans bibliothèque de routage.
import { useEffect, useState } from 'react';
import type { NavItem } from '@/components/AppLayout';

export type Screen = 'simulation' | 'configurer';

const SCREENS: { id: Screen; label: string }[] = [
	{ id: 'simulation', label: 'Simulation' },
	{ id: 'configurer', label: 'Configurer' },
];

function screenFromHash(): Screen {
	return window.location.hash === '#configurer' ? 'configurer' : 'simulation';
}

export function useScreen(): Screen {
	const [screen, setScreen] = useState<Screen>(screenFromHash);

	useEffect(() => {
		const onChange = () => setScreen(screenFromHash());
		window.addEventListener('hashchange', onChange);
		return () => window.removeEventListener('hashchange', onChange);
	}, []);

	return screen;
}

export function navFor(current: Screen): NavItem[] {
	return SCREENS.map((s) => ({ label: s.label, href: `#${s.id}`, active: s.id === current }));
}
