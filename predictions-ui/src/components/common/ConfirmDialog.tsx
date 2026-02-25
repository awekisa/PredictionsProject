import { useRef } from 'react';
import styles from './ConfirmDialog.module.css';

interface Props {
  message: string;
  onConfirm: () => void;
  onCancel: () => void;
}

export default function ConfirmDialog({ message, onConfirm, onCancel }: Props) {
  const openedAt = useRef(Date.now());

  const handleOverlayClick = () => {
    if (Date.now() - openedAt.current < 300) return;
    onCancel();
  };

  return (
    <div className={styles.overlay} onClick={handleOverlayClick}>
      <div className={styles.card} onClick={(e) => e.stopPropagation()}>
        <p className={styles.message}>{message}</p>
        <div className={styles.actions}>
          <button className={styles.cancelBtn} onClick={onCancel}>
            Cancel
          </button>
          <button className={styles.deleteBtn} onClick={onConfirm}>
            Delete
          </button>
        </div>
      </div>
    </div>
  );
}
