import { BookOpen, GraduationCap, School } from "lucide-react";
import type { SchoolType } from "@/types/school";
import { cn } from "@/lib/utils";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

interface SchoolTypeDialogProps {
  open: boolean;
  onClose: () => void;
  onSelect: (type: SchoolType) => void;
}

const schoolTypes: {
  type: SchoolType;
  label: string;
  icon: typeof GraduationCap;
  description: string;
  accent: string;
  iconWrap: string;
}[] = [
  {
    type: "kindergarten",
    label: "幼儿园",
    icon: GraduationCap,
    description:
      "适合 3-6 岁儿童的学前教育机构，重点关注环境、安全与启蒙体验。",
    accent: "hover:border-peach-200 hover:bg-peach-50/70",
    iconWrap: "bg-peach-100 text-peach-500",
  },
  {
    type: "primary",
    label: "小学",
    icon: BookOpen,
    description:
      "适合 6-12 岁儿童的基础教育阶段，关注师资、口碑与综合成长环境。",
    accent: "hover:border-sky-200 hover:bg-sky-50/70",
    iconWrap: "bg-sky-100 text-sky-500",
  },
  {
    type: "middle",
    label: "中学",
    icon: School,
    description:
      "适合 12-18 岁青少年的中等教育阶段，兼顾成绩表现与校园活跃度。",
    accent: "hover:border-sage-200 hover:bg-sage-50/70",
    iconWrap: "bg-sage-100 text-sage-500",
  },
];

export function SchoolTypeDialog({
  open,
  onClose,
  onSelect,
}: SchoolTypeDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(value) => !value && onClose()}>
      <DialogContent className="gap-0 overflow-hidden p-0">
        <div className="border-b border-border/70 px-6 pb-5 pt-8 sm:px-8">
          <DialogHeader>
            <DialogTitle>选择学校类型</DialogTitle>
            <DialogDescription>
              先确定您要查找的教育阶段，系统会基于位置与公开信息生成对应推荐结果。
            </DialogDescription>
          </DialogHeader>
        </div>

        <div className="space-y-3 px-5 py-5 sm:px-6 sm:py-6">
          {schoolTypes.map((item) => (
            <button
              key={item.type}
              type="button"
              onClick={() => onSelect(item.type)}
              className={cn(
                "group flex w-full items-start gap-4 rounded-[26px] border border-border/70 bg-background/70 p-4 text-left transition-all duration-200",
                item.accent,
              )}
            >
              <div
                className={cn(
                  "flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl",
                  item.iconWrap,
                )}
              >
                <item.icon className="h-5 w-5" />
              </div>
              <div className="min-w-0 flex-1">
                <h3 className="text-base font-semibold text-neutral-900">
                  {item.label}
                </h3>
                <p className="mt-1 text-sm leading-7 text-neutral-600">
                  {item.description}
                </p>
              </div>
              <div className="mt-1 rounded-full border border-border/70 px-3 py-1 text-xs text-neutral-500 transition-colors group-hover:border-warm-200 group-hover:text-warm-700">
                选择
              </div>
            </button>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  );
}
