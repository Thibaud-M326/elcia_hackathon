import type { ButtonHTMLAttributes } from 'react';
import styles from '@/components/Button.module.css';

type Variant = 'primary' | 'secondary' | 'danger';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
	variant?: Variant;
}

export default function Button({ variant = 'secondary', className, type = 'button', ...rest }: ButtonProps) {
	const classes = [styles.button, styles[variant], className].filter(Boolean).join(' ');
	return <button type={type} className={classes} {...rest} />;
}
