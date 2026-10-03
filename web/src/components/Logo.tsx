type LogoProps = {
  size?: number;
  className?: string;
};

//the logo file is square, so one size drives both width and height and nothing shifts on load
export function Logo({ size = 60, className }: LogoProps) {
  return <img src="/carbonate-logo.png" alt="Carbonate" width={size} height={size} className={className} />;
}
