import { memo } from "react";

interface FormattedTitleProps {
    text: string;
}

function escapeHtml(text: string): string {
    return text
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");
}

function applyInlineFormatting(text: string): string {
    // Apply minimal markdown-style formatting and strip the markers
    return text
        .replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>")
        .replace(/__(.+?)__/g, "<strong>$1</strong>")
        .replace(/\*(.+?)\*/g, "<em>$1</em>")
        .replace(/_(.+?)_/g, "<em>$1</em>")
        .replace(/`(.+?)`/g, "<code>$1</code>");
}

function FormattedTitle({ text }: FormattedTitleProps) {
    const html = applyInlineFormatting(escapeHtml(text));
    return <span dangerouslySetInnerHTML={{ __html: html }} />;
}

export default memo(FormattedTitle);
