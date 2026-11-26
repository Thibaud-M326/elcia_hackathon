import type { ReactNode } from 'react';
import styles from '@/components/AppLayout.module.css';

export interface NavItem {
	label: string;
	href: string;
	active?: boolean;
}

interface AppLayoutProps {
	title: string;
	breadcrumb?: string;
	nav: NavItem[];
	confirmLeave?: () => boolean;
	children: ReactNode;
}

export default function AppLayout({ title, breadcrumb, nav, confirmLeave, children }: AppLayoutProps) {
	return (
		<div className={styles.layout}>
			<aside className={styles.sidebar}>
				<div className={styles.logo}>42 PRICER</div>
				<nav aria-label="Menu principal">
					<ul className={styles.nav}>
						{nav.map((item) => (
							<li key={item.href}>
								<a
									href={item.href}
									className={`${styles.navLink}${item.active ? ` ${styles.active}` : ''}`}
									aria-current={item.active ? 'page' : undefined}
									onClick={(e) => {
										if (!item.active && confirmLeave && !confirmLeave()) e.preventDefault();
									}}
								>
									{item.label}
								</a>
							</li>
						))}
					</ul>
				</nav>
			</aside>

			<div className={styles.main}>
				<header className={styles.header}>
					<h1 className={styles.title}>{title}</h1>
					{breadcrumb && <p className={styles.breadcrumb}>{breadcrumb}</p>}
				</header>
				<main className={styles.content}>{children}</main>
			</div>
		</div>
	);
}
