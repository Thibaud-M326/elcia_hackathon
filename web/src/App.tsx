import Configurer from '@/pages/Configurer';
import Products from '@/pages/Products';
import Simulation from '@/pages/Simulation';
import { useRoute } from '@/nav';

export default function App() {
	const { screen, productId } = useRoute();

	// key : changer de produit recrée l'écran (pas d'état du produit précédent)
	if (screen === 'configurer' && productId) return <Configurer key={productId} productId={productId} />;
	if (screen === 'simulation' && productId) return <Simulation key={productId} productId={productId} />;
	return <Products />;
}
