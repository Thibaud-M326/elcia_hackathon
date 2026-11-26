import type { ReactNode } from 'react';
import styles from '@/components/Badge.module.css';

type Tone = 'info' | 'success' | 'warning' | 'error';

export default function Badge({ tone, children }: { tone: Tone; children: ReactNode }) {
	return <span className={`${styles.badge} ${styles[tone]}`}>{children}</span>;
}
