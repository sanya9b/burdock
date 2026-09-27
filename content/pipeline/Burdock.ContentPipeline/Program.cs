using System.Globalization;

// burdock-content: the content pipeline CLI (spec 08 §3.6). Commands are added stage by stage; unknown commands exit with 2.
var commands = new (string Name, string Description)[]
{
    ("import cefrj", "Import the CEFR-J word list (lemma, POS, level) with the citation string from content/sources"),
    ("import oewn", "Attach Open English WordNet senses"),
    ("import cmudict", "Attach ARPAbet pronunciations and derive GA IPA"),
    ("classify", "Assign topics with the LLM classifier and confidence scores"),
    ("translate", "Generate Ukrainian translation candidates by sense"),
    ("examples", "Generate level-appropriate example sentences"),
    ("review ai", "Run the second-model review rubric"),
    ("review queue", "Export the human review queue (100% free pool, 10% stratified)"),
    ("validate", "Validate schema, licenses, forbidden sources and Ukrainian typography rules"),
    ("pack build", "Build JSON.gz packs per level and topic with SHA-256"),
    ("manifest update", "Update the manifest and publish to object storage"),
};

if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
{
    Console.WriteLine("burdock-content <command> [options]");
    Console.WriteLine();
    foreach (var (name, description) in commands)
    {
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0,-18} {1}", name, description));
    }

    return 0;
}

Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, "Command '{0}' is not implemented yet. Run with --help for the planned command list.", string.Join(' ', args)));
return 2;
