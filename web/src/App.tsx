import Configurer from '@/pages/Configurer';
import Simulation from '@/pages/Simulation';
import { useScreen } from '@/nav';

const PRODUCT_ID = 'fenetre-pvc-2v';

export default function App() {
	const screen = useScreen();
	return screen === 'configurer' ? <Configurer productId={PRODUCT_ID} /> : <Simulation productId={PRODUCT_ID} />;
}
