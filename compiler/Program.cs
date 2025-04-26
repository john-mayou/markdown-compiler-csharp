using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

var app = WebApplication.Create(args);

app.MapGet("/", async context =>
{
  var markdown = File.ReadAllText("example.text");
  var compiledHtml = new Compiler.Compiler().Compile(markdown);
  context.Response.ContentType = "text/html";
  await context.Response.WriteAsync(WrapHtml(compiledHtml));
});

string WrapHtml(string body)
{
  return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Markdown Preview</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; }}
        blockquote {{ color: gray; border-left: 4px solid #ccc; padding-left: 10px; }}
        strong {{ font-weight: bold; }}
    </style>
</head>
<body>
    {body}
</body>
</html>";
}

app.Run();
