namespace Compiler.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using FluentAssertions;

public class CompilerTests
{
    [Fact]
    public void TokenizeParagraph()
    {
        var expected = new List<Lexer.Token> {
            new Lexer.TextToken("text", false, false),
            new Lexer.NewLineToken()
        };
        var actual = new Compiler().Tokenize("text");

        actual.Select(x => x.ToString()).Should().BeEquivalentTo(expected.Select(x => x.ToString()));
    }

    [Fact]
    public void ParseParagraph()
    {
        var expected = new Parser.ASTRootNode(new List<Parser.ASTNode> {
            new Parser.ASTParagraphNode(new List<Parser.ASTNode> {
                new Parser.ASTTextNode("text", false, false)
            })
        });
        var actual = new Compiler().Parse(new List<Lexer.Token> {
            new Lexer.TextToken("text", false, false),
            new Lexer.NewLineToken()
        });

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GenParagraph()
    {
        var expected = "<p>text</p>";
        var actual = new Compiler().Gen(
            new Parser.ASTRootNode(new List<Parser.ASTNode> {
                new Parser.ASTParagraphNode(new List<Parser.ASTNode> {
                    new Parser.ASTTextNode("text", false, false)
                })
            })
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GoldenFiles()
    {
        bool update = Environment.GetEnvironmentVariable("UPDATE") == "true";
        var failures = new List<string>();

        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../"));
        var testDataPath = Path.Combine(projectDir, "testdata");
        var files = Directory.GetFiles(testDataPath, "*.text");

        foreach (var filepath in files)
        {
            string testName = "golden_" + Path.GetFileName(filepath);
            try
            {
                string md = File.ReadAllText(filepath);
                string html = PrettifyHtml(new Compiler().Compile(md));
                string htmlPath = Path.Combine("testdata", Path.GetFileNameWithoutExtension(filepath) + ".html");

                if (update)
                {
                    File.WriteAllText(htmlPath, html);
                }

                string actual = File.ReadAllText(htmlPath);
                if (actual != html)
                {
                    failures.Add($"{testName}\n - Expected:\n{html}\n - Actual:\n{actual}");
                }
            }
            catch (Exception e)
            {
                failures.Add($"{testName} - Exception thrown: {e}");
            }
        }

        Assert.True(failures.Count == 0, $"File test failure:\n{string.Join("\n=======================\n", failures)}");
    }

    private string PrettifyHtml(string html)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "prettier",
                Arguments = "--parser html",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();

        using (var writer = process.StandardInput)
        {
            writer.Write(html);
        }

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0 || !string.IsNullOrEmpty(error))
        {
            throw new Exception($"Prettier command failed: {error}");
        }

        return output;
    }
}