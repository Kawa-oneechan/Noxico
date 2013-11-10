using System.Collections.Generic;
using System.Linq;

// TODO: Add a user interface!

namespace Noxico
{
	public class Crafting
	{
		public static Character Carrier { get; private set; }
		public static Token ItemsToWorkWith { get; private set; }

		/// <summary>
		/// Returns a list of possible Recipes that a given character is able to craft.
		/// </summary>
		/// <param name="carrier">Probably the player character.</param>
		/// <returns>A list of possible recipes. Keep it, and invoke Apply on the player's choice.</returns>
		public static List<Recipe> GetPossibilities(Character carrier)
		{
			Carrier = carrier;
			ItemsToWorkWith = Carrier.GetToken("items");
			var results = new List<Recipe>();
			var tml = Mix.GetTokenTree("crafting.tml");
			foreach (var carriedItem in ItemsToWorkWith.Tokens)
			{
				var knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == carriedItem.Name);
				foreach (var craft in tml.Where(t => t.Name == "craft"))
				{
					var requirements = new List<Token>();
					var key = craft.GetToken("key").Tokens[0];
					var craftOkay = true;
					var considerations = new List<Token>();
					if (key.Name.StartsWith("@"))
					{
						var r = key.Name.Substring(1);
						if (!knownItem.HasToken(r))
							continue;
					}
					else if (key.Name != knownItem.ID)
						continue;

					var repeat = false;
					do
					{
						requirements.Clear();
						requirements.Add(carriedItem);
						repeat = false;
						foreach (var requirement in craft.GetToken("require").Tokens)
						{
							if (requirement.Name.StartsWith("@"))
							{
								var r = requirement.Name.Substring(1);
								var firstValidItem = ItemsToWorkWith.Tokens.Find(i2ww => NoxicoGame.KnownItems.FirstOrDefault(ki => ki.ID == i2ww.Name && ki.HasToken(r)) != null && !considerations.Contains(i2ww));
								if (firstValidItem == null)
								{
									craftOkay = false;
									break;
								}
								requirements.Add(firstValidItem);
								considerations.Add(firstValidItem);
								repeat = true;
							}
							else
							{
								if (!ItemsToWorkWith.HasToken(requirement.Name))
								{
									craftOkay = false;
									break;
								}
								requirements.Add(ItemsToWorkWith.GetToken(requirement.Name));
							}
						}

						if (!craftOkay)
							continue;

						var recipe = new Recipe();
						var actions = craft.GetToken("actions");
						foreach (var action in actions.Tokens)
						{
							if (action.Name == "consume")
							{
								var itemIndex = (int)action.Value - 1;
								if (itemIndex >= requirements.Count)
								{
									Program.WriteLine("Warning: recipe wants to consume out-of-bounds requirement.");
									continue;
								}
								recipe.Actions.Add(new CraftConsumeItemAction() { Target = requirements[itemIndex] });
							}
							else if (action.Name == "produce")
							{
								var itemMade = new Token(action.Text);
								itemMade.Tokens.AddRange(action.Tokens);
								recipe.Actions.Add(new CraftProduceItemAction() { Target = itemMade });
								knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == itemMade.Name);
								recipe.Display = string.Format("Create {0}", knownItem.ToString(itemMade)); //TRANSLATE
							}
							else if (action.Name == "dye")
							{
								var color = NoxicoGame.KnownItems.Find(ki => ki.ID == requirements[0].Name).GetToken("color").Text;
								knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == requirements[1].Name);
								if (!requirements[1].HasToken("color"))
									recipe.Actions.Add(new CraftAddTokenAction() { Target = requirements[1], Add = new Token("color", 0, color) });
								else
									recipe.Actions.Add(new CraftChangeTokenAction() { Target = requirements[1].GetToken("color"), NewText = color, NewValue = requirements[1].Value });
								recipe.Display = string.Format("Dye {0} {1}", knownItem.ToString(requirements[1]), color); //TRANSLATE
							}
						}
						if (string.IsNullOrWhiteSpace(recipe.Display))
							continue;
						results.Add(recipe);
					} while (repeat);
				}
			}
			return results;
		}
	}

	public class Recipe
	{
		public string Display { get; set; }
		public List<CraftAction> Actions { get; set; }
		public Recipe()
		{
			Actions = new List<CraftAction>();
		}
		public void Apply()
		{
			Actions.ForEach(a => a.Apply());
		}
		public override string ToString()
		{
			return Display;
		}
	}

	public abstract class CraftAction
	{
		public Token Target { get; set; }
		public abstract void Apply();
	}
	public class CraftProduceItemAction : CraftAction
	{
		public override void Apply()
		{
			Crafting.ItemsToWorkWith.AddToken(Target);
		}
	}
	public class CraftConsumeItemAction : CraftAction
	{
		public override void Apply()
		{
			var knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == Target.Name);
			knownItem.Consume(Crafting.Carrier, Target);
		}
	}
	public class CraftChangeTokenAction : CraftAction
	{
		public string NewText { get; set; }
		public float NewValue { get; set; }
		public override void Apply()
		{
			Target.Text = NewText;
			Target.Value = NewValue;
		}
	}
	public class CraftRemoveTokenAction : CraftAction
	{
		public Token Remove { get; set; }
		public override void Apply()
		{
			Target.RemoveToken(Remove);
		}
	}
	public class CraftAddTokenAction : CraftAction
	{
		public Token Add { get; set; }
		public override void Apply()
		{
			Target.AddToken(Add);
		}
	}
}
