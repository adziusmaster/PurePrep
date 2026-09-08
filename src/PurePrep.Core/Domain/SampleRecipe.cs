namespace PurePrep.Domain;

/// <summary>
/// The recipe seeded into a brand-new library on first launch, so the app is never an empty box.
///
/// It is a Polish classic — pierogi ruskie — as a nod to where PurePrep comes from, but it is shown
/// in English by default so it reads for everyone. A Polish translation is pre-cached, so switching
/// the recipe to Polish from the translate menu is instant and free (no credit, no network) — a
/// small showcase of the translation feature on content we already trust.
/// </summary>
public static class SampleRecipe
{
    public static ParsedRecipe Create()
    {
        var steps = new[]
        {
            "Make the dough: mix the flour with the warm water, egg and salt, then knead for about 8 minutes until smooth and elastic. Cover and rest for 30 minutes.",
            "Meanwhile, boil the peeled potatoes in salted water until tender, about 20 minutes, then drain well and mash until smooth.",
            "Gently fry the finely chopped onion in butter over low heat until soft and golden, about 10 minutes.",
            "Stir the fried onion and the twaróg (farmer's cheese) into the mashed potato. Season generously with salt and pepper and let the filling cool.",
            "Roll the dough out thinly and cut out circles with a glass. Place a spoon of filling on each, fold over and pinch the edges firmly to seal.",
            "Cook the pierogi in batches in a large pot of gently boiling salted water. They are ready about 2 minutes after they float to the top.",
            "Serve hot, topped with a little more butter or with onions fried until golden. Smacznego!",
        };

        var recipe = new ParsedRecipe
        {
            Title = "Polish Pierogi Ruskie (Potato & Cheese Dumplings)",
            Ingredients = new[]
            {
                "300 g plain flour, plus extra for rolling",
                "150 ml warm water",
                "1 egg",
                "1 tsp salt",
                "500 g floury potatoes, peeled",
                "250 g twaróg (Polish farmer's cheese) or ricotta",
                "1 large onion, finely chopped",
                "2 tbsp butter",
                "Salt and black pepper, to taste",
            },
            Steps = steps
                .Select((instruction, index) => new RecipeStep { Order = index + 1, Instruction = instruction })
                .ToArray(),
            SourceSystem = MeasurementSystem.Metric,
            OriginalLanguage = "en",
        };

        return recipe.WithTranslation("pl", PolishTranslation(), originalLanguage: "en")
            // The recipe is stored so English is what shows by default; Polish is one free tap away.
            .WithDisplayLanguage(null);
    }

    private static RecipeTranslation PolishTranslation() => new()
    {
        Title = "Pierogi ruskie (z ziemniakami i twarogiem)",
        Ingredients = new[]
        {
            "300 g mąki pszennej, plus trochę do podsypywania",
            "150 ml ciepłej wody",
            "1 jajko",
            "1 łyżeczka soli",
            "500 g ziemniaków (mączystych), obranych",
            "250 g twarogu",
            "1 duża cebula, drobno posiekana",
            "2 łyżki masła",
            "Sól i pieprz do smaku",
        },
        Steps = new[]
        {
            "Zagnieć ciasto: wymieszaj mąkę z ciepłą wodą, jajkiem i solą, a następnie wyrabiaj przez około 8 minut, aż będzie gładkie i elastyczne. Przykryj i odstaw na 30 minut.",
            "W międzyczasie ugotuj obrane ziemniaki w osolonej wodzie do miękkości, około 20 minut, odcedź i dokładnie utłucz na gładką masę.",
            "Delikatnie podsmaż drobno posiekaną cebulę na maśle na małym ogniu, aż zmięknie i się zezłoci, około 10 minut.",
            "Wmieszaj podsmażoną cebulę i twaróg do ziemniaków. Dopraw obficie solą i pieprzem, a następnie ostudź farsz.",
            "Rozwałkuj ciasto cienko i wykrawaj krążki szklanką. Na każdy nałóż łyżkę farszu, złóż na pół i mocno zlep brzegi.",
            "Gotuj pierogi partiami w dużym garnku delikatnie wrzącej, osolonej wody. Są gotowe około 2 minuty po tym, jak wypłyną na powierzchnię.",
            "Podawaj na gorąco, polane odrobiną masła lub ze zrumienioną cebulką. Smacznego!",
        }
        .Select((instruction, index) => new RecipeStep { Order = index + 1, Instruction = instruction })
        .ToArray(),
    };
}
