﻿using System.Collections.Generic;
using Grid;
using UnityEngine;

public class TileGridCell : MonoBehaviour
{
	[SerializeField] private TileGridCellData data = null;
	private TileGrid grid = null;
	[SerializeField] private Animator anim;

	[SerializeField] private GameObject fxRoot = null;
	[SerializeField] public GameObject tileContainer = null;

	public TileGrid Grid
	{
		get { return grid; }
		set { grid = value; }
	}

	public TileGridCellData Data
	{
		get { return data; }
		set { data = value; }
	}

	private Tile tile = null;
	public Tile awaitingFallingTile = null;

	public Tile Tile
	{
		get { return tile; }
		set
		{
			if (tile != value)
			{
				tile = value;
				if (tile != null)
				{
					tile.GridCell = this;
				}
			}
		}
	}

	public bool TileReady => Tile != null
		? Tile.LockedIn
		: false;

	public IEnumerable<TileGridCell> CardinalNeighbors()
	{
		if (data.y < Grid.GridHeight - 1)
		{
			// Up
			yield return Grid.Cell(data.x, data.y + 1);
		}

		if (data.x < Grid.GridWidth - 1)
		{
			// Right
			yield return Grid.Cell(data.x + 1, data.y);
		}

		if (data.y > 0)
		{
			// Down
			yield return Grid.Cell(data.x, data.y - 1);
		}

		if (data.x > 0)
		{
			// Left
			yield return Grid.Cell(data.x - 1, data.y);
		}
	}

	public IEnumerable<TileGridCell> NonCardinalNeighbors()
	{
		bool hasUp = data.y < Grid.GridHeight;
		bool hasRight = data.x < Grid.GridWidth;
		bool hasDown = data.y > 0;
		bool hasLeft = data.x > 0;

		if (hasUp && hasRight)
		{
			// Up-Right
			yield return Grid.Cell(data.x + 1, data.y + 1);
		}

		if (hasRight && hasDown)
		{
			// Right-Down
			yield return Grid.Cell(data.x + 1, data.y - 1);
		}

		if (hasDown && hasLeft)
		{
			// Down-Left
			yield return Grid.Cell(data.x - 1, data.y - 1);
		}

		if (hasLeft && hasUp)
		{
			// Left-Up
			yield return Grid.Cell(data.x - 1, data.y + 1);
		}
	}

	public IEnumerable<TileGridCell> AllNeighbors()
	{
		bool hasUp = data.y < Grid.GridHeight;
		bool hasRight = data.x < Grid.GridWidth;
		bool hasDown = data.y > 0;
		bool hasLeft = data.x > 0;

		if (hasUp)
		{
			// Up
			yield return Grid.Cell(data.x, data.y + 1);
			if (hasRight)
			{
				// Up-Right
				yield return Grid.Cell(data.x + 1, data.y + 1);
			}
		}

		if (hasRight)
		{
			// Right
			yield return Grid.Cell(data.x + 1, data.y);
			if (hasDown)
			{
				// Right-Down
				yield return Grid.Cell(data.x + 1, data.y - 1);
			}
		}

		if (hasDown)
		{
			// Down
			yield return Grid.Cell(data.x, data.y - 1);
			if (hasLeft)
			{
				// Down-Left
				yield return Grid.Cell(data.x - 1, data.y - 1);
			}
		}

		if (hasLeft)
		{
			// Left
			yield return Grid.Cell(data.x - 1, data.y);
			if (hasUp)
			{
				// Left-Up
				yield return Grid.Cell(data.x - 1, data.y + 1);
			}
		}
	}

	public bool IsNeighbor(TileGridCell other, bool requireCardinal)
	{
		if (other == null || other == this)
		{
			return false;
		}

		if (requireCardinal)
		{
			foreach (var neighbor in CardinalNeighbors())
			{
				if (neighbor == other)
				{
					return true;
				}
			}
		}
		else
		{
			foreach (var neighbor in AllNeighbors())
			{
				if (neighbor == other)
				{
					return true;
				}
			}
		}

		return false;
	}


	public void HandleGridChange()
	{
		if (Tile != null)
		{
			Tile.HandleGridChange();
		}
	}

	public Tile GenerateTile(TileData tileData)
	{
		if (tile == null)
		{
			tile = Instantiate(Grid.TilePrefab, tileContainer.transform).GetComponent<Tile>();
		}

		tile.GridCell = this;
		tile.Data = tileData;

		tile.Data.OnSpawn(tile, Grid);

		return tile;
	}

	public bool CanBeginSwap()
	{
		if (!TileReady)
		{
			return false;
		}

		return Grid != null
			? Grid.CanBeginSwap(this)
			: false;
	}

	public void MakeSwapTarget()
	{
		if (Grid != null)
		{
			Grid.ChooseSwapTarget(this);
		}
	}

	public void UnmakeSwapTarget()
	{
		if (Tile != null)
		{
			Tile.Event_Unselect();
		}
	}

	public void SwapTile()
	{
		if (Grid.CanSwapTileWithSelected(this))
		{
			if (anim != null)
			{
				Grid.PrepareSwapTileWithSelected(this);
			}
			else
			{
				Event_FinishSwap();
			}
		}
	}

    public void AnimateMove(string trigger)
	{
		if (anim != null)
		{
            AudioManager.Instance.PlaySwapAudio();
            anim.SetTrigger(trigger);
		}
		else
		{
			Event_FinishSwap();
		}
	}

	public void Event_FinishSwap()
	{
		Grid.SwapTileWithSelected(this);
	}

	public void CheckForTriplet()
	{
		if (InTriplet())
		{
            AudioManager.Instance.PlayBeat();
            BuildDestructionLists(null, 0);
		}
	}

	public bool InTriplet()
	{
		if (Tile == null)
		{
			return false;
		}

		int matches = 0;
		foreach (var neighbor in CardinalNeighbors())
		{
			if (neighbor != null && neighbor.Tile != null)
			{
				if (IsValidMatch(neighbor))
				{
					return neighbor.MatchInLine(this);
				}
			}
		}

		return false;
	}

	public bool MatchInLine(TileGridCell other)
	{
		if (other == this || other == null)
		{
			return false;
		}

		int xDiff = Data.x - other.Data.x;
		int yDiff = Data.y - other.Data.y;

		// Only count nearby neigbors.
		if (Mathf.Abs(xDiff) + Mathf.Abs(yDiff) > 1)
		{
			return false;
		}

		var oppositeNeighbor = Grid.Cell(Data.x + xDiff, Data.y + yDiff);
		if (oppositeNeighbor != null)
		{
			if (oppositeNeighbor.IsValidMatch(other))
			{
				return true;
			}
		}

		var otherOppositeNeighbor = Grid.Cell(other.Data.x - xDiff, other.Data.y - yDiff);
		if (otherOppositeNeighbor)
		{
			if (otherOppositeNeighbor.IsValidMatch(this))
			{
				return true;
			}
		}

		return false;
	}

	private bool IsValidMatch(TileGridCell other)
	{
		if ((other != null && other != this)
		    && (other.Tile != null && other.TileReady)
		    && (Tile != null && TileReady))
		{
			return other.Tile.IsMatch(Tile);
		}

		return false;
	}

	public void BuildDestructionLists(TileGridCell starter, int order)
	{
		// TODO This method thing is very expensive... maybe we can optimize it.

		if (Grid == null)
		{
			return;
		}


		if (starter == null)
		{
			if (Grid.PrepareToDetonate(this, 0, this))
			{
				order = 0;
				starter = this;
			}
			else
			{
				// A list starting with cell already exists, don't try starting a new one.
				return;
			}
		}

		var recurseNeighbors = new List<TileGridCell>();

		foreach (var neighbor in CardinalNeighbors())
		{
			if (neighbor != null && neighbor.IsValidMatch(this))
			{
				if (MatchInLine(neighbor))
				{
					if (Grid.PrepareToDetonate(neighbor, order + 1, starter))
					{
						recurseNeighbors.Add(neighbor);
					}
				}

			}
		}

		foreach (var neighbor in AllNeighbors())
		{
			if (neighbor != null && neighbor.Tile != null)
			{

			}
		}

		foreach (var neighbor in recurseNeighbors)
		{
			neighbor.BuildDestructionLists(starter, order++);
		}
	}

	public TileGridCell GetCellBelow()
	{
		return Grid.GetCellBelow(this);
	}

	public void ShowDetonate(GameObject detonateFX)
	{
		if (detonateFX != null && fxRoot != null)
		{
			Instantiate(detonateFX, fxRoot.transform);
		}
	}

	public void ShowBurn(GameObject burnFX)
	{
		if (burnFX != null && fxRoot != null)
		{
			Instantiate(burnFX, fxRoot.transform);
		}
	}
}
