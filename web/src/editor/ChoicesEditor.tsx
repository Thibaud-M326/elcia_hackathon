import { useState } from 'react';
import Button from '@/components/Button';
import fieldStyles from '@/components/Field.module.css';
import styles from '@/editor/Editor.module.css';

interface ChoicesEditorProps {
	id: string;
	choices: string[];
	defaultChoice: string | null;
	onChange: (choices: string[], defaultChoice: string | null) => void;
}

// Liste des choix d'un paramètre "choice" : ajouter, supprimer, choisir le défaut
export default function ChoicesEditor({ id, choices, defaultChoice, onChange }: ChoicesEditorProps) {
	const [draft, setDraft] = useState('');
	const trimmed = draft.trim();
	const duplicate = choices.includes(trimmed);

	function add() {
		if (!trimmed || duplicate) return;
		onChange([...choices, trimmed], defaultChoice ?? trimmed);
		setDraft('');
	}

	function remove(choice: string) {
		const next = choices.filter((c) => c !== choice);
		onChange(next, defaultChoice === choice ? (next[0] ?? null) : defaultChoice);
	}

	return (
		<fieldset className={styles.fieldset}>
			<legend className={fieldStyles.label}>Choix possibles (le choix coché est celui par défaut)</legend>

			{choices.length === 0 ? (
				<p className={styles.empty}>Aucun choix pour l'instant.</p>
			) : (
				<ul className={styles.choices}>
					{choices.map((choice) => (
						<li key={choice} className={styles.choice}>
							<label className={styles.choiceLabel}>
								<input
									type="radio"
									name={`${id}-default`}
									checked={defaultChoice === choice}
									onChange={() => onChange(choices, choice)}
								/>
								{choice}
							</label>
							<Button variant="danger" className={styles.small} onClick={() => remove(choice)} aria-label={`Supprimer le choix ${choice}`}>
								Supprimer
							</Button>
						</li>
					))}
				</ul>
			)}

			<div className={styles.addRow}>
				<input
					id={`${id}-new`}
					type="text"
					value={draft}
					placeholder="Nouveau choix"
					aria-label="Nouveau choix"
					className={fieldStyles.control}
					onChange={(e) => setDraft(e.target.value)}
					onKeyDown={(e) => {
						if (e.key === 'Enter') {
							e.preventDefault();
							add();
						}
					}}
				/>
				<Button onClick={add} disabled={!trimmed || duplicate}>
					Ajouter
				</Button>
			</div>
			{duplicate && trimmed && <span className={fieldStyles.error}>Ce choix existe déjà.</span>}
		</fieldset>
	);
}
