﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Licensed under MIT. See LICENSE in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    internal sealed class NonAsciiChractersAreEscapedInLiterals : IFormattingRule
    {
        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken) as CompilationUnitSyntax;

            if (root == null)
                return document;

            var newRoot = UnicodeCharacterEscapingSyntaxRewriter.Rewriter.Visit(root);

            return document.WithSyntaxRoot(newRoot);
        }

        /// <summary>
        ///  Rewrites string and character literals which contain non ascii characters to instead use the \uXXXX or \UXXXXXXXX syntax.
        /// </summary>
        internal class UnicodeCharacterEscapingSyntaxRewriter : CSharpSyntaxRewriter
        {
            public static readonly UnicodeCharacterEscapingSyntaxRewriter Rewriter = new UnicodeCharacterEscapingSyntaxRewriter();

            private UnicodeCharacterEscapingSyntaxRewriter()
            {
            }

            public override SyntaxNode VisitLiteralExpression(LiteralExpressionSyntax node)
            {
                switch (node.CSharpKind())
                {
                    case SyntaxKind.StringLiteralExpression:
                        return RewriteStringLiteralExpression(node);
                    case SyntaxKind.CharacterLiteralExpression:
                        return RewriteCharacterLiteralExpression(node);
                }

                return base.Visit(node);
            }

            private static SyntaxNode RewriteStringLiteralExpression(LiteralExpressionSyntax node)
            {
                Debug.Assert(node.CSharpKind() == SyntaxKind.StringLiteralExpression);

                if (HasNonAsciiCharacters(node.Token.Text))
                {
                    string convertedText = EscapeNonAsciiCharacters(node.Token.Text);

                    SyntaxToken t = SyntaxFactory.Literal(node.Token.LeadingTrivia, convertedText, node.Token.ValueText, node.Token.TrailingTrivia);

                    node = node.WithToken(t);
                }

                return node;
            }

            private static SyntaxNode RewriteCharacterLiteralExpression(LiteralExpressionSyntax node)
            {
                Debug.Assert(node.CSharpKind() == SyntaxKind.CharacterLiteralExpression);

                if (HasNonAsciiCharacters(node.Token.Text))
                {
                    string convertedText = EscapeNonAsciiCharacters(node.Token.Text);

                    SyntaxToken t = SyntaxFactory.Literal(node.Token.LeadingTrivia, convertedText, node.Token.ValueText, node.Token.TrailingTrivia);

                    node = node.WithToken(t);
                }

                return node;
            }


            private static bool HasNonAsciiCharacters(string value)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    if (value[i] >= 0x80)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static string EscapeNonAsciiCharacters(string oldValue)
            {
                StringBuilder sb = new StringBuilder(oldValue.Length);

                for (int i = 0; i < oldValue.Length; i++)
                {
                    if (oldValue[i] < 0x80)
                    {
                        sb.Append(oldValue[i]);
                    }
                    else if (char.IsHighSurrogate(oldValue[i]) && i + 1 < oldValue.Length && char.IsLowSurrogate(oldValue[i + 1]))
                    {
                        sb.Append(string.Format(@"\U{0:X8}", char.ConvertToUtf32(oldValue[i], oldValue[i + 1])));
                        i++; // move past the low surogate we consumed above.
                    }
                    else
                    {
                        sb.Append(string.Format(@"\u{0:X4}", (ushort)oldValue[i]));
                    }
                }

                return sb.ToString();
            }
        }
    }
}