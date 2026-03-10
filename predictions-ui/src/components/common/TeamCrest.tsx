import { useState, useEffect, useRef } from 'react';
import { localCrestPath } from '../../utils/crestUrl';

interface Props {
  teamName: string;
  fallbackUrl?: string | null;
  className?: string;
  localSrc?: string;
  localFallbackSrc?: string;
}

export default function TeamCrest({ teamName, fallbackUrl, className, localSrc, localFallbackSrc }: Props) {
  const [srcIndex, setSrcIndex] = useState(0);
  const prevLocalSrc = useRef(localSrc);

  useEffect(() => {
    if (prevLocalSrc.current !== localSrc) {
      prevLocalSrc.current = localSrc;
      setSrcIndex(0);
    }
  }, [localSrc]);

  const sources = [
    localSrc ?? localCrestPath(teamName),
    localFallbackSrc,
    fallbackUrl,
  ].filter((s): s is string => !!s);

  const src = sources[srcIndex];

  if (!src) return null;

  return (
    <img
      src={src}
      alt=""
      className={className}
      onError={() => {
        if (srcIndex < sources.length - 1) {
          setSrcIndex(srcIndex + 1);
        }
      }}
    />
  );
}
