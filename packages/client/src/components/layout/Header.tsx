import { useState } from "react";
import { GraduationCap, Menu, Sparkles, X } from "lucide-react";
import { motion, AnimatePresence } from "framer-motion";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

interface HeaderProps {
  className?: string;
}

const navItems = [
  { href: "#features", label: "能力亮点" },
  { href: "#workflow", label: "使用流程" },
  { href: "#about", label: "数据说明" },
];

export function Header({ className }: HeaderProps) {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  return (
    <>
      <header
        className={cn(
          "fixed inset-x-0 top-0 z-50 px-3 pt-3 sm:px-6",
          className,
        )}
      >
        <div className="surface-panel mx-auto flex h-16 w-full max-w-6xl items-center justify-between rounded-full px-4 sm:px-6">
          <a href="#top" className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-full bg-linear-to-br from-warm-500 to-peach-400 text-white shadow-[0_12px_24px_rgba(224,122,63,0.28)]">
              <GraduationCap className="h-5 w-5" />
            </div>
            <div>
              <p className="text-[11px] font-medium uppercase tracking-[0.24em] text-warm-600">
                Geo School Match
              </p>
              <h1 className="font-display text-base font-semibold text-neutral-900 sm:text-lg">
                学校推荐系统
              </h1>
            </div>
          </a>

          <nav className="hidden items-center gap-1 md:flex">
            {navItems.map((item) => (
              <a
                key={item.href}
                href={item.href}
                className="rounded-full px-4 py-2 text-sm text-neutral-600 transition-colors hover:bg-accent hover:text-warm-700"
              >
                {item.label}
              </a>
            ))}
          </nav>

          <div className="hidden items-center gap-3 md:flex">
            <div className="flex items-center gap-2 rounded-full bg-warm-100 px-3 py-1.5 text-xs font-medium text-warm-700">
              <Sparkles className="h-3.5 w-3.5" />
              AI 多源分析
            </div>
            <Button asChild variant="warm" size="sm">
              <a href="#hero-start">开始择校</a>
            </Button>
          </div>

          <button
            type="button"
            aria-label={mobileMenuOpen ? "关闭菜单" : "打开菜单"}
            onClick={() => setMobileMenuOpen((value) => !value)}
            className="flex h-10 w-10 items-center justify-center rounded-full border border-border/70 bg-background/80 text-neutral-700 md:hidden"
          >
            {mobileMenuOpen ? (
              <X className="h-5 w-5" />
            ) : (
              <Menu className="h-5 w-5" />
            )}
          </button>
        </div>
      </header>

      <AnimatePresence>
        {mobileMenuOpen && (
          <motion.div
            initial={{ opacity: 0, y: -12 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -12 }}
            transition={{ duration: 0.18 }}
            className="fixed inset-x-3 top-22 z-40 md:hidden"
          >
            <div className="surface-panel rounded-[28px] p-4">
              <nav className="flex flex-col gap-1">
                {navItems.map((item) => (
                  <a
                    key={item.href}
                    href={item.href}
                    onClick={() => setMobileMenuOpen(false)}
                    className="rounded-2xl px-4 py-3 text-sm text-neutral-700 transition-colors hover:bg-accent hover:text-warm-700"
                  >
                    {item.label}
                  </a>
                ))}
                <Button asChild variant="warm" className="mt-3 w-full">
                  <a
                    href="#hero-start"
                    onClick={() => setMobileMenuOpen(false)}
                  >
                    开始择校
                  </a>
                </Button>
              </nav>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </>
  );
}
