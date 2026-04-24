using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Turnierspielplan
{
    public partial class MainWindow : Window
    {
        private sealed class Team
        {
            public string Name { get; set; }
            public string ImagePath { get; set; }
        }

        // A per-slot view of a team: same Team object, but IsAvailable is computed
        // against the slot this item will appear in. Bound from the DataTemplate.
        private sealed class TeamSlotItem
        {
            public Team Team { get; set; }
            public bool IsAvailable { get; set; }
            public int UsedInSlotIndex { get; set; } = -1;

            public string Name => Team.Name;
            public string ImagePath => Team.ImagePath;
            public string StatusText => IsAvailable ? string.Empty : $"in Slot {UsedInSlotIndex + 1}";
        }

        private static readonly Random _rng = new Random();

        private readonly List<Team> _allTeams = new List<Team>
        {
            new Team { Name = "Barcelona",            ImagePath = "/Turnierspielplan;component/images/barca.png"    },
            new Team { Name = "Real Madrid",          ImagePath = "/Turnierspielplan;component/images/real.png"     },
            new Team { Name = "Bayern Munich",        ImagePath = "/Turnierspielplan;component/images/bayern.png"   },
            new Team { Name = "Paris Saint-Germain",  ImagePath = "/Turnierspielplan;component/images/paris.png"    },
            new Team { Name = "Atletico Madrid",      ImagePath = "/Turnierspielplan;component/images/atletico.png" },
            new Team { Name = "Juventus",             ImagePath = "/Turnierspielplan;component/images/juventus.jpg" },
            new Team { Name = "Manchester City",      ImagePath = "/Turnierspielplan;component/images/mancity.png"  },
            new Team { Name = "Chelsea",              ImagePath = "/Turnierspielplan;component/images/chelsea.png"  },
        };

        private ComboBox[] _slots;
        private bool _refreshing;
        private int _simulationStage;

        public MainWindow()
        {
            InitializeComponent();

            _slots = new[]
            {
                Mannschaft1, Mannschaft2, Mannschaft3, Mannschaft4,
                Mannschaft5, Mannschaft6, Mannschaft7, Mannschaft8
            };

            RefreshAllSlots();
        }

        // ----------------------------------------------------------------
        // Centralized selection state management
        // ----------------------------------------------------------------

        private void Slot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshing) return;
            RefreshAllSlots();
        }

        /// <summary>
        /// Rebuilds every slot's item list from the single source of truth
        /// (<see cref="_allTeams"/> + current SelectedValue of every slot).
        /// Each team appears in every slot, but is disabled in slots where it
        /// is already used elsewhere. Deselecting/replacing a team in one slot
        /// immediately re-enables it everywhere else.
        /// </summary>
        private void RefreshAllSlots()
        {
            _refreshing = true;
            try
            {
                // Snapshot of "team name -> slot index that owns it" before rebuilding.
                var owners = new Dictionary<string, int>();
                for (int i = 0; i < _slots.Length; i++)
                {
                    var name = _slots[i].SelectedValue as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        owners[name] = i;
                    }
                }

                for (int i = 0; i < _slots.Length; i++)
                {
                    var slot = _slots[i];
                    var currentName = slot.SelectedValue as string;

                    var items = new List<TeamSlotItem>(_allTeams.Count);
                    foreach (var team in _allTeams)
                    {
                        int ownerIndex;
                        bool ownedByOther = owners.TryGetValue(team.Name, out ownerIndex)
                                            && ownerIndex != i;
                        items.Add(new TeamSlotItem
                        {
                            Team = team,
                            IsAvailable = !ownedByOther,
                            UsedInSlotIndex = ownedByOther ? ownerIndex : -1,
                        });
                    }

                    slot.ItemsSource = items;

                    if (currentName != null)
                    {
                        slot.SelectedItem = items.FirstOrDefault(it => it.Team.Name == currentName);
                    }
                }
            }
            finally
            {
                _refreshing = false;
            }
        }

        // ----------------------------------------------------------------
        // Simulation (unchanged behavior)
        // ----------------------------------------------------------------

        private void btnSimulieren_Click(object sender, RoutedEventArgs e)
        {
            _simulationStage++;

            string nextLabel;
            switch (_simulationStage)
            {
                case 1:
                    if (!AllSlotsSelected())
                    {
                        MessageBox.Show(
                            "Bitte wählen Sie alle Mannschaften aus.",
                            "Fehlende Auswahl",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        _simulationStage--;
                        return;
                    }
                    SimulateMatch(Mannschaft1.SelectedValue.ToString(), Mannschaft2.SelectedValue.ToString(), lblTore1, lblTore2);
                    SimulateMatch(Mannschaft3.SelectedValue.ToString(), Mannschaft4.SelectedValue.ToString(), lblTore3, lblTore4);
                    SimulateMatch(Mannschaft5.SelectedValue.ToString(), Mannschaft6.SelectedValue.ToString(), lblTore5, lblTore6);
                    SimulateMatch(Mannschaft7.SelectedValue.ToString(), Mannschaft8.SelectedValue.ToString(), lblTore7, lblTore8);
                    nextLabel = "Wer kommt in den Final?";
                    LockSlots(true);
                    break;

                case 2:
                    SimulateMatch(lblErsteGewinner1.Content?.ToString(), lblErsteGewinner2.Content?.ToString(), lblErsteGewinner1Tore, lblErsteGewinner2Tore);
                    SimulateMatch(lblErsteGewinner3.Content?.ToString(), lblErsteGewinner4.Content?.ToString(), lblErsteGewinner3Tore, lblErsteGewinner4Tore);
                    nextLabel = "Wer ist der Sieger?";
                    break;

                case 3:
                    SimulateMatch(lblFinal1.Content?.ToString(), lblFinal2.Content?.ToString(), lblFinal1Tore, lblFinal2Tore);
                    DetermineWinner();
                    nextLabel = "Fertig";
                    btnSimulieren.IsEnabled = false;
                    break;

                default:
                    return;
            }

            btnSimulieren.Content = nextLabel;
        }

        private bool AllSlotsSelected()
        {
            return _slots.All(s => s.SelectedValue != null);
        }

        private void LockSlots(bool locked)
        {
            foreach (var slot in _slots)
            {
                slot.IsEnabled = !locked;
            }
        }

        private void SimulateMatch(string team1, string team2, Label lblTeam1Score, Label lblTeam2Score)
        {
            int score1 = _rng.Next(11);
            int score2 = _rng.Next(11);

            if (score1 == score2)
            {
                int tieBreaker = _rng.Next(4);
                if      (tieBreaker == 0 && score1 > 0) score1--;
                else if (tieBreaker == 1 && score2 > 0) score2--;
                else if (tieBreaker == 2)               score1++;
                else                                    score2++;
            }

            lblTeam1Score.Content = score1.ToString();
            lblTeam2Score.Content = score2.ToString();
        }

        private void DetermineWinner()
        {
            var final1 = lblFinal1.Content?.ToString();
            var final2 = lblFinal2.Content?.ToString();
            if (!int.TryParse(lblFinal1Tore.Content?.ToString(), out int score1)) return;
            if (!int.TryParse(lblFinal2Tore.Content?.ToString(), out int score2)) return;

            lblSieger.Content = score1 >= score2 ? final1 : final2;
        }
    }
}
