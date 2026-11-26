import { useId, useState } from 'react';
import type { ReactNode } from 'react';
import styles from '@/components/Accordion.module.css';

interface AccordionProps {
	title: string;
	amount?: string;
	muted?: boolean;
	defaultOpen?: boolean;
	children: ReactNode;
}

export default function Accordion({ title, amount, muted = false, defaultOpen = false, children }: AccordionProps) {
	const [open, setOpen] = useState(defaultOpen);
	const panelId = useId();

	return (
		<section className={styles.accordion}>
			<h3 className={styles.heading}>
				<button
					type="button"
					className={`${styles.trigger}${muted ? ` ${styles.muted}` : ''}`}
					aria-expanded={open}
					aria-controls={panelId}
					onClick={() => setOpen((o) => !o)}
				>
					<svg className={styles.icon} width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
						<rect x="3" y="3" width="18" height="18" rx="3" />
						<path d="M8 12h8" />
						{!open && <path d="M12 8v8" />}
					</svg>
					<span className={styles.title}>{title}</span>
					{amount && <span className={styles.amount}>{amount}</span>}
				</button>
			</h3>
			<div id={panelId} className={styles.panel} hidden={!open}>
				{children}
			</div>
		</section>
	);
}
