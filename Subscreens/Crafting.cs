using System.Collections.Generic;
using System.Linq;

//TODO: Rework with a less dumb format, allow requiring more of the same item (several instances/charges)

namespace Noxico
{
	/// <summary>
	/// Displays the crafting interface.
	/// </summary>
	public class Crafting
	{
		/// <summary>
		/// The character doing the crafting. Set by GetPossibilities and Open.
		/// </summary>
		public static Character Carrier { get; private set; }
		/// <summary>
		/// The Carrier's items. Set by GetPossibilities.
		/// </summary>
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
			foreach (var recipe in tml)
			{
				var considered = new List<Token>();
				var resultName = "?";
				if (recipe.HasToken("produce"))
					resultName = recipe.GetToken("produce").Text;
				var resultingSourceItem = default(InventoryItem);

				var steps = recipe.Tokens;
				var stepRefs = new Token[steps.Count()];
				var i = 0;
				var newRecipe = new Recipe();
				foreach (var step in steps)
				{
					if (step.Name == "consume" || step.Name == "require")
					{
						var amount = 1;
						if (step.HasToken("amount"))
							amount = (int)step.GetToken("amount").Value;

						var numFound = 0;
						if (step.Text == "<anything>")
						{
							foreach (var carriedItem in ItemsToWorkWith.Tokens)
							{
								var knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == carriedItem.Name);
								if (knownItem == null)
								{
									Program.WriteLine("Crafting: don't know what a {0} is.", carriedItem.Name);
									continue;
								}

								var withs = step.GetAll("with");
								var withouts = step.GetAll("withouts");
								var okay = 0;
								foreach (var with in withs)
								{
									if (knownItem.HasToken(with.Text))
										okay++;
								}
								foreach (var without in withouts)
								{
									if (knownItem.HasToken(without.Text))
										okay = 0;
								}
								if (okay < withs.Count())
									continue;

								if (carriedItem.HasToken("charge"))
									numFound += (int)carriedItem.GetToken("charge").Value;
								else
									numFound++;
								stepRefs[i] = carriedItem;

								if (i == 0)
									resultingSourceItem = knownItem;
							}
						}
						else if (step.Text == "book")
						{
							//TODO: see if a book with the given ID is marked as read.
						}
						else
						{
							foreach (var carriedItem in ItemsToWorkWith.Tokens.Where(t => t.Name == step.Text))
							{
								var knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == carriedItem.Name);
								if (knownItem == null)
								{
									Program.WriteLine("Crafting: don't know what a {0} is.", carriedItem.Name);
									continue;
								}

								if (carriedItem.HasToken("charge"))
									numFound += (int)carriedItem.GetToken("charge").Value;
								else
									numFound++;
								stepRefs[i] = carriedItem;

								if (i == 0)
									resultingSourceItem = knownItem;
							}
						}

						if (numFound < amount)
						{
							Program.WriteLine("Crafting: not enough {0} to craft {1}.", step.Text, resultName);
							break;
						}
						if (step.Name == "consume")
							newRecipe.Actions.Add(new CraftConsumeItemAction() { Target = stepRefs[i] });
					}
					else if (step.Name == "produce")
					{
						var itemMade = new Token(step.Text);
						itemMade.Tokens.AddRange(step.Tokens);
						newRecipe.Actions.Add(new CraftProduceItemAction() { Target = itemMade });
						var resultingKnownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == itemMade.Name);
						newRecipe.Display = i18n.Format("craft_produce_x_from_y", resultingKnownItem.ToString(itemMade), resultingSourceItem.ToString(stepRefs[0]));
					}
					else if (step.Name == "train")
					{
						newRecipe.Actions.Add(new CraftTrainAction() { Trainee = carrier, Target = step });
					}
				}
				if (newRecipe.Display.IsBlank())
					continue;
				if (results.Exists(x => x.Display == newRecipe.Display))
					continue;
				results.Add(newRecipe);
			}
			
			//Find dyes
			foreach (var maybeDye in ItemsToWorkWith.Tokens)
			{
				var knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == maybeDye.Name);
				if (knownItem == null)
				{
					Program.WriteLine("Crafting: don't know what a {0} is.", maybeDye.Name);
					continue;
				}

				if (knownItem.HasToken("dye"))
				{
					var dyeItem = maybeDye;
					var color = dyeItem.GetToken("color").Text;
					foreach (var carriedItem in ItemsToWorkWith.Tokens)
					{
						knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == carriedItem.Name);
						if (knownItem == null)
						{
							Program.WriteLine("Crafting: don't know what a {0} is.", carriedItem.Name);
							continue;
						}

						if (knownItem.HasToken("colored") && !knownItem.HasToken("dye"))
						{
							var newRecipe = new Recipe();
							newRecipe.Display = i18n.Format("craft_dye_x_y", knownItem.ToString(carriedItem), color);
							if (!carriedItem.HasToken("color"))
								newRecipe.Actions.Add(new CraftAddTokenAction() { Target = carriedItem, Add = new Token("color", 0, color) });
							else if (carriedItem.GetToken("color").Text == color)
								continue;
							else
								newRecipe.Actions.Add(new CraftChangeTokenAction() { Target = carriedItem.GetToken("color"), NewText = color, NewValue = 0 });
							results.Add(newRecipe);
						}
					}
				}
			}
			return results;
		}

		private static UIWindow recipeWindow;
		private static UIList recipeList;
		private static List<Recipe> recipes;

		/// <summary>
		/// Generic Subscreen handler.
		/// </summary>
		public static void Handler()
		{
			//TODO: add more things, such as a status bar and a window with the exact results.
			if (Subscreens.FirstDraw)
			{
				UIManager.Initialize();
				Subscreens.FirstDraw = false;
				NoxicoGame.ClearKeys();
				Subscreens.Redraw = true;
			}
			if (Subscreens.Redraw)
			{
				Subscreens.Redraw = false;
				recipes = GetPossibilities(Carrier);
				var h = recipes.Count < 40 ? recipes.Count : 40;
				if (h == 0)
					h++;
				recipeWindow = new UIWindow(string.Empty) { Top = 2, Left = 2, Width = 76, Height = h + 2 };
				recipeList = new UIList(string.Empty, null, recipes.Select(r => r.Display)) { Top = 3, Left = 3, Width = 74, Height = h };
				recipeList.Enter = (s, e) =>
				{
					if (recipes.Count == 0)
						return;
					var i = recipeList.Index;
					recipes[i].Apply();
					//Redetermine the possibilities and update the UI accordingly.
					recipes = GetPossibilities(Carrier);
					if (recipes.Count > 0)
					{
						recipeList.Items = recipes.Select(r => r.Display).ToList();
						if (i >= recipeList.Items.Count)
							i = recipeList.Items.Count - 1;
						recipeList.Index = i;
						h = recipes.Count < 40 ? recipes.Count : 40;
					}
					else
					{
						h = 1;
						recipeList.Hidden = true;
					}
					recipeWindow.Height = h + 2;
					recipeList.Height = h;
					NoxicoGame.Me.CurrentBoard.Redraw();
					//NoxicoGame.Me.CurrentBoard.Draw();
					UIManager.Draw();
				};
				UIManager.Elements.Add(recipeWindow);
				UIManager.Elements.Add(new UILabel(i18n.GetString("craft_nothing")) { Left = 3, Top = 3 });
				UIManager.Elements.Add(recipeList);
				UIManager.Highlight = recipeList;
				UIManager.Draw();
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.Back) || NoxicoGame.IsKeyDown(KeyBinding.Items) || Vista.Triggers == XInputButtons.B)
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Immediate = true;
				NoxicoGame.Me.CurrentBoard.Redraw();
				//NoxicoGame.Me.CurrentBoard.Draw(true);
				NoxicoGame.Mode = UserMode.Walkabout;
				Subscreens.FirstDraw = true;
			}
			else
				UIManager.CheckKeys();
		}

		/// <summary>
		/// Sets up a Crafting subscreen for the given Character, usually the Player.
		/// </summary>
		/// <param name="carrier"></param>
		public static void Open(Character carrier)
		{
			Carrier = carrier;
			NoxicoGame.Subscreen = Handler;
			NoxicoGame.Mode = UserMode.Subscreen;
			Subscreens.FirstDraw = true;
			NoxicoGame.ClearKeys();
		}
	}

	/// <summary>
	/// Defines a single crafting recipe, a single resulting item.
	/// </summary>
	public class Recipe
	{
		/// <summary>
		/// The display name of this recipe's result.
		/// </summary>
		public string Display { get; set; }
		/// <summary>
		/// A list of CraftActions that produce this recipe's result.
		/// </summary>
		public List<CraftAction> Actions { get; set; }
		public Recipe()
		{
			Actions = new List<CraftAction>();
		}
		/// <summary>
		/// Simply applies each CraftAction for this Recipe.
		/// </summary>
		public void Apply()
		{
			Actions.ForEach(a => a.Apply());
		}
		public override string ToString()
		{
			return Display;
		}
	}

	/// <summary>
	/// Base class for CraftActions.
	/// </summary>
	public abstract class CraftAction
	{
		/// <summary>
		/// The Token that this CraftAction applies to.
		/// </summary>
		public Token Target { get; set; }
		/// <summary>
		/// What to do with the Target.
		/// </summary>
		public abstract void Apply();
	}
	/// <summary>
	/// Adds the CraftAction's Target to the Carrier's item list.
	/// </summary>
	public class CraftProduceItemAction : CraftAction
	{
		public override void Apply()
		{
			Crafting.ItemsToWorkWith.AddToken(Target);
		}
	}
	/// <summary>
	/// Consumes an item with the Target's name in the Carrier's inventory.
	/// </summary>
	public class CraftConsumeItemAction : CraftAction
	{
		public override void Apply()
		{
			var knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == Target.Name);
			knownItem.Consume(Crafting.Carrier, Target);
		}
	}
	/// <summary>
	/// Changes the Target's Text and/or Value.
	/// </summary>
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
	/// <summary>
	/// Removes a Token from the Target's children.
	/// </summary>
	public class CraftRemoveTokenAction : CraftAction
	{
		public Token Remove { get; set; }
		public override void Apply()
		{
			Target.RemoveToken(Remove);
		}
	}
	/// <summary>
	/// Adds a Token to the Target's children.
	/// </summary>
	public class CraftAddTokenAction : CraftAction
	{
		public Token Add { get; set; }
		public override void Apply()
		{
			Target.AddToken(Add);
		}
	}
	/// <summary>
	/// Trains the crafter in a skill
	/// </summary>
	public class CraftTrainAction : CraftAction
	{
		public Character Trainee { get; set; }
		public override void Apply()
		{
			Trainee.IncreaseSkill(Target.Text);
		}
	}
}
