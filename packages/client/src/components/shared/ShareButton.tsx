import { useState, useCallback } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { Share2, Check, Link2 } from "lucide-react";
import { Button } from "@/components/ui/button";

interface ShareButtonProps {
  /** 要复制的 URL；默认取 window.location.href */
  url?: string;
  className?: string;
}

export function ShareButton({ url, className }: ShareButtonProps) {
  const [copied, setCopied] = useState(false);

  const handleShare = useCallback(async () => {
    const targetUrl = url ?? window.location.href;

    // 优先尝试 Web Share API（移动端原生分享）
    if (navigator.share) {
      try {
        await navigator.share({
          title: "学校推荐结果",
          text: "我在学校推荐系统中找到了不错的学校，快来看看吧！",
          url: targetUrl,
        });
        return;
      } catch {
        // 用户取消或不支持，回退到复制链接
      }
    }

    // 复制到剪贴板
    try {
      await navigator.clipboard.writeText(targetUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // fallback
      const input = document.createElement("input");
      input.value = targetUrl;
      document.body.appendChild(input);
      input.select();
      document.execCommand("copy");
      document.body.removeChild(input);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  }, [url]);

  return (
    <Button
      variant="ghost"
      size="sm"
      onClick={handleShare}
      className={className}
      aria-label="分享结果"
    >
      <AnimatePresence mode="wait">
        {copied ? (
          <motion.div
            key="copied"
            initial={{ scale: 0.5, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            exit={{ scale: 0.5, opacity: 0 }}
            className="flex items-center gap-1 text-sage-600"
          >
            <Check className="w-4 h-4" />
            <span className="text-xs">已复制</span>
          </motion.div>
        ) : (
          <motion.div
            key="share"
            initial={{ scale: 0.5, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            exit={{ scale: 0.5, opacity: 0 }}
            className="flex items-center gap-1"
          >
            <Share2 className="w-4 h-4" />
            <span className="text-xs hidden sm:inline">分享</span>
          </motion.div>
        )}
      </AnimatePresence>
    </Button>
  );
}
