namespace PurePrep.Domain;

/// <summary>
/// The recipe seeded into a brand-new library on first launch, so the app is never an empty box.
///
/// It is a Polish classic — pierogi ruskie (potato &amp; cheese dumplings), based on Ania Gotuje's
/// recipe (no egg in the dough) — as a nod to where PurePrep comes from, but it is shown in English
/// by default so it reads for everyone. A Polish translation is pre-cached, so switching the recipe
/// to Polish from the translate menu is instant and free (no credit, no network). It carries a full
/// set of 1.4 features (servings, prep/cook time, named step timers, ingredient references, a note)
/// so it doubles as a showcase of what an imported recipe looks like once PurePrep has parsed it.
/// </summary>
public static class SampleRecipe
{
    public static ParsedRecipe Create()
    {
        var ingredients = new[]
        {
            "650 g potatoes (about 500 g once cooked)",
            "½ tsp salt (for the filling)",
            "½ tsp pepper",
            "300 g onion",
            "2 tbsp clarified butter",
            "300 g semi-fat cottage cheese (twaróg)",
            "500 g plain flour",
            "½ tsp salt (for the dough)",
            "50 ml oil",
            "250 ml hot water",
            "2 tbsp butter, for frying (optional)",
        };

        var steps = new[]
        {
            new RecipeStep
            {
                Order = 1,
                Instruction = "This makes about 60 pierogi. Peel the potatoes and boil them until tender, " +
                    "then drain, let the steam escape and mash them. Season with salt and pepper and leave " +
                    "to cool.",
                IngredientRefs = [0, 1, 2],
                Timers = [new RecipeTimer("Boil potatoes", 1200, 1500)],
            },
            new RecipeStep
            {
                Order = 2,
                Instruction = "Chop the onion finely and fry it gently in the clarified butter until soft " +
                    "and golden.",
                IngredientRefs = [3, 4],
                Timers = [new RecipeTimer("Fry onion", 600, 600)],
            },
            new RecipeStep
            {
                Order = 3,
                Instruction = "Press the cottage cheese through a sieve or potato ricer, then mix it with " +
                    "the cooled potatoes and half of the fried onion. Taste and adjust the seasoning.",
                IngredientRefs = [5, 0, 3],
            },
            new RecipeStep
            {
                Order = 4,
                Instruction = "Mix the flour and salt in a large bowl, add the oil and the hot water, stir " +
                    "with a spoon, then knead into a smooth, elastic dough (no egg needed).",
                IngredientRefs = [6, 7, 8, 9],
                Timers = [new RecipeTimer("Knead dough", 480, 480)],
            },
            new RecipeStep
            {
                Order = 5,
                Instruction = "Wrap the dough and let it rest so it rolls out without shrinking back.",
                Timers = [new RecipeTimer("Rest dough", 1800, 1800)],
            },
            new RecipeStep
            {
                Order = 6,
                Instruction = "Roll the dough out thinly in batches, cut out 8 cm circles, put a teaspoon " +
                    "of filling on each, fold and pinch the edges firmly shut. Keep finished pierogi under " +
                    "a damp cloth.",
                IngredientRefs = [6],
            },
            new RecipeStep
            {
                Order = 7,
                Instruction = "Boil the pierogi in batches in salted water; once they float, cook them a " +
                    "little longer, then lift them out.",
                Timers = [new RecipeTimer("Boil pierogi", 180, 240)],
            },
            new RecipeStep
            {
                Order = 8,
                Instruction = "Serve with the remaining fried onion, or for extra crunch fry the boiled " +
                    "pierogi in butter until golden on both sides.",
                IngredientRefs = [3, 10],
                Timers = [new RecipeTimer("Fry pierogi", 180, 240)],
            },
        };

        var recipe = new ParsedRecipe
        {
            Title = "Pierogi ruskie (potato & cheese dumplings)",
            SourceUrl = "https://aniagotuje.pl/przepis/pierogi-ruskie",
            Ingredients = ingredients,
            Steps = steps,
            SourceSystem = MeasurementSystem.Metric,
            OriginalLanguage = "en",
            Servings = 6,
            ServingsEstimated = false,
            PrepMinutes = 60,
            CookMinutes = 30,
            Status = RecipeStatus.WantToCook,
            ImagePath = null,
            Notes = "Tip: freeze uncooked pierogi on a tray for about 90 minutes, then bag them — boil " +
                "straight from frozen.",
        };

        return recipe.WithTranslation("pl", PolishTranslation(), originalLanguage: "en")
            // The recipe is stored so English is what shows by default; Polish is one free tap away.
            .WithDisplayLanguage(null);
    }

    private static RecipeTranslation PolishTranslation() => new()
    {
        Title = "Pierogi ruskie",
        Ingredients = new[]
        {
            "650 g ziemniaków (ok. 500 g po ugotowaniu)",
            "½ łyżeczki soli (do farszu)",
            "½ łyżeczki pieprzu",
            "300 g cebuli",
            "2 łyżki masła klarowanego",
            "300 g twarogu półtłustego",
            "500 g mąki pszennej",
            "½ łyżeczki soli (do ciasta)",
            "50 ml oleju",
            "250 ml gorącej wody",
            "2 łyżki masła do smażenia (opcjonalnie)",
        },
        Steps = new[]
        {
            new RecipeStep
            {
                Order = 1,
                Instruction = "Wychodzi około 60 pierogów. Obierz ziemniaki i ugotuj do miękkości, odcedź, " +
                    "odparuj i utłucz na gładko. Dopraw solą i pieprzem i odstaw do wystygnięcia.",
                IngredientRefs = [0, 1, 2],
                Timers = [new RecipeTimer("Gotowanie ziemniaków", 1200, 1500)],
            },
            new RecipeStep
            {
                Order = 2,
                Instruction = "Cebulę drobno posiekaj i podsmaż delikatnie na maśle klarowanym, aż zmięknie " +
                    "i się zezłoci.",
                IngredientRefs = [3, 4],
                Timers = [new RecipeTimer("Smażenie cebuli", 600, 600)],
            },
            new RecipeStep
            {
                Order = 3,
                Instruction = "Przeciśnij twaróg przez sitko lub praskę do ziemniaków, wymieszaj z " +
                    "wystudzonymi ziemniakami i połową usmażonej cebuli. Dopraw do smaku.",
                IngredientRefs = [5, 0, 3],
            },
            new RecipeStep
            {
                Order = 4,
                Instruction = "Wymieszaj mąkę z solą, dodaj olej i gorącą wodę, wymieszaj łyżką, a " +
                    "następnie zagnieć na gładkie, elastyczne ciasto (bez jajka).",
                IngredientRefs = [6, 7, 8, 9],
                Timers = [new RecipeTimer("Zagniatanie ciasta", 480, 480)],
            },
            new RecipeStep
            {
                Order = 5,
                Instruction = "Zawiń ciasto i odstaw, żeby odpoczęło — dzięki temu rozwałkuje się bez " +
                    "kurczenia.",
                Timers = [new RecipeTimer("Odpoczynek ciasta", 1800, 1800)],
            },
            new RecipeStep
            {
                Order = 6,
                Instruction = "Rozwałkuj ciasto cienko partiami, wykrawaj krążki o średnicy 8 cm, na każdy " +
                    "nałóż łyżeczkę farszu, złóż i mocno zlep brzegi. Gotowe pierogi trzymaj pod wilgotną " +
                    "ściereczką.",
                IngredientRefs = [6],
            },
            new RecipeStep
            {
                Order = 7,
                Instruction = "Gotuj pierogi partiami w osolonej wodzie; gdy wypłyną, gotuj je jeszcze " +
                    "chwilę, a potem wyjmij.",
                Timers = [new RecipeTimer("Gotowanie pierogów", 180, 240)],
            },
            new RecipeStep
            {
                Order = 8,
                Instruction = "Podawaj z pozostałą cebulką, a dla większego chrupania usmaż ugotowane " +
                    "pierogi na maśle na złoto z obu stron.",
                IngredientRefs = [3, 10],
                Timers = [new RecipeTimer("Smażenie pierogów", 180, 240)],
            },
        },
    };
}
