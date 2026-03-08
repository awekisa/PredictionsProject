import { useState } from 'react';
import { localCrestPath } from '../../utils/crestUrl';

interface Props {
  teamName: string;
  fallbackUrl?: string | null;
  className?: string;
}

export default function TeamCrest({ teamName, fallbackUrl, className }: Props) {
  const [useFallback, setUseFallback] = useState(false);

  const src = useFallback ? fallbackUrl : localCrestPath(teamName);

  if (!src) return null;

  return (
    <img
      src={src}
      alt=""
      className={className}
      onError={() => {
        if (!useFallback && fallbackUrl) {
          setUseFallback(true);
        }
      }}
    />
  );
}
