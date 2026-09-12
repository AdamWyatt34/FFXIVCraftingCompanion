//! The search tree as two chunked arenas: 48-byte nodes holding a packed state, and 16-byte
//! edges holding the visit statistics. A node's edges are one contiguous slice reserved at its
//! first expansion, so selecting a child reads one slice instead of chasing sibling pointers, and
//! a chunk is never moved once allocated, so growth costs one chunk allocation and no copying.

use crate::actions::{Action, ActionId};
use crate::solver::ActionSet;
use crate::state::CraftingState;

use super::packed::PackedState;

/// Sentinel for "no node" and "no edge slice" in every link field.
pub const NONE: u32 = u32::MAX;

const CHUNK_SHIFT: u32 = 16;
/// Entries per chunk: 65 536, so nodes grow in 3 MB steps and edges in 1 MB steps, sizes a wasm
/// heap can grow by without copying.
pub const CHUNK_ENTRIES: usize = 1 << CHUNK_SHIFT;
const CHUNK_MASK: usize = CHUNK_ENTRIES - 1;

#[derive(Clone, Debug)]
struct Chunked<T> {
    chunks: Vec<Vec<T>>,
    len: usize,
}

impl<T: Copy + Default> Default for Chunked<T> {
    fn default() -> Chunked<T> {
        Chunked {
            chunks: Vec::new(),
            len: 0,
        }
    }
}

impl<T: Copy + Default> Chunked<T> {
    fn clear(&mut self) {
        for chunk in &mut self.chunks {
            chunk.clear();
        }
        self.len = 0;
    }

    fn get(&self, index: u32) -> &T {
        let index = index as usize;
        &self.chunks[index >> CHUNK_SHIFT][index & CHUNK_MASK]
    }

    fn get_mut(&mut self, index: u32) -> &mut T {
        let index = index as usize;
        &mut self.chunks[index >> CHUNK_SHIFT][index & CHUNK_MASK]
    }

    fn push(&mut self, value: T) -> u32 {
        let chunk = self.len >> CHUNK_SHIFT;
        if chunk == self.chunks.len() {
            self.chunks.push(Vec::with_capacity(CHUNK_ENTRIES));
        }
        self.chunks[chunk].push(value);
        self.len += 1;
        (self.len - 1) as u32
    }

    /// Appends `count` default entries that are guaranteed to share a chunk, padding the current
    /// chunk out when it cannot hold them. `count` must not exceed a chunk.
    fn reserve_contiguous(&mut self, count: usize) -> u32 {
        let used = self.len & CHUNK_MASK;
        if used != 0 && used + count > CHUNK_ENTRIES {
            for _ in used..CHUNK_ENTRIES {
                self.push(T::default());
            }
        }
        let start = self.len as u32;
        for _ in 0..count {
            self.push(T::default());
        }
        start
    }

    fn slice(&self, start: u32, len: usize) -> &[T] {
        let start = start as usize;
        &self.chunks[start >> CHUNK_SHIFT][start & CHUNK_MASK..(start & CHUNK_MASK) + len]
    }
}

/// The link from a parent to one child, carrying the child's statistics (the C# node's
/// `VisitCount` and `TotalReward`). Edge 0 of each root belongs to the root itself.
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct Edge {
    pub child: u32,
    pub visits: u32,
    pub total_reward: f64,
}

impl Edge {
    pub fn average_reward(&self) -> f64 {
        if self.visits > 0 {
            self.total_reward / self.visits as f64
        } else {
            0.0
        }
    }
}

#[derive(Clone, Copy, Debug, Default)]
pub struct Node {
    pub packed: PackedState,
    pub parent: u32,
    /// The edge from `parent` to this node; the root's own edge for the root.
    pub parent_edge: u32,
    /// Start of this node's edge slice, `NONE` until the first child is added.
    pub edge_start: u32,
    pub untried: ActionSet,
    pub edge_len: u8,
    pub edge_cap: u8,
    /// The action that produced this node; `None` at the root.
    pub action: Option<Action>,
}

impl Node {
    pub fn is_terminal(&self) -> bool {
        !self.packed.is_in_progress()
    }

    pub fn is_fully_expanded(&self) -> bool {
        self.untried.is_empty()
    }
}

#[derive(Clone, Debug, Default)]
pub struct Arena {
    nodes: Chunked<Node>,
    edges: Chunked<Edge>,
    /// Source of the per-solve maxima the packed states leave out.
    base: CraftingState,
}

impl Arena {
    /// Drops every node and edge but keeps the chunks for the next solve.
    pub fn clear(&mut self) {
        self.nodes.clear();
        self.edges.clear();
    }

    pub fn len(&self) -> usize {
        self.nodes.len
    }

    pub fn is_empty(&self) -> bool {
        self.nodes.len == 0
    }

    pub fn edge_count(&self) -> usize {
        self.edges.len
    }

    /// Bytes held by nodes and edges, padding included.
    pub fn memory_bytes(&self) -> usize {
        self.nodes.len * core::mem::size_of::<Node>()
            + self.edges.len * core::mem::size_of::<Edge>()
    }

    pub fn get(&self, index: u32) -> &Node {
        self.nodes.get(index)
    }

    pub fn get_mut(&mut self, index: u32) -> &mut Node {
        self.nodes.get_mut(index)
    }

    pub fn state(&self, index: u32) -> CraftingState {
        self.nodes.get(index).packed.unpack(&self.base)
    }

    /// The statistics of `index` (its incoming edge).
    pub fn stats(&self, index: u32) -> Edge {
        *self.edges.get(self.nodes.get(index).parent_edge)
    }

    /// The first root pushed after a `clear` fixes the maxima every packed state is read against.
    pub fn push_root(&mut self, state: CraftingState, untried: ActionSet) -> u32 {
        if self.nodes.len == 0 {
            self.base = state;
        }
        let edge = self.edges.push(Edge::default());
        let index = self.nodes.push(Node {
            packed: PackedState::pack(&state),
            parent: NONE,
            parent_edge: edge,
            edge_start: NONE,
            untried,
            edge_len: 0,
            edge_cap: 0,
            action: None,
        });
        self.edges.get_mut(edge).child = index;
        index
    }

    /// Adds the child reached by `action`, which must still be untried on the parent; the
    /// parent's edge slice is reserved on the first call, sized to its legal actions.
    pub fn push_child(
        &mut self,
        parent: u32,
        action: Action,
        state: CraftingState,
        untried: ActionSet,
    ) -> u32 {
        let parent_node = *self.nodes.get(parent);
        assert!(
            parent_node.untried.contains(action),
            "{action:?} is not untried on node {parent}"
        );
        let (edge_start, edge_cap) = if parent_node.edge_start == NONE {
            let cap = parent_node.untried.len();
            (self.edges.reserve_contiguous(cap), cap as u8)
        } else {
            (parent_node.edge_start, parent_node.edge_cap)
        };
        debug_assert!(parent_node.edge_len < edge_cap);

        let edge = edge_start + parent_node.edge_len as u32;
        let index = self.nodes.push(Node {
            packed: PackedState::pack(&state),
            parent,
            parent_edge: edge,
            edge_start: NONE,
            untried,
            edge_len: 0,
            edge_cap: 0,
            action: Some(action),
        });
        *self.edges.get_mut(edge) = Edge {
            child: index,
            visits: 0,
            total_reward: 0.0,
        };

        let parent_node = self.nodes.get_mut(parent);
        parent_node.untried.remove(action);
        parent_node.edge_start = edge_start;
        parent_node.edge_cap = edge_cap;
        parent_node.edge_len += 1;
        index
    }

    fn edges_of(&self, parent: u32) -> &[Edge] {
        let node = self.nodes.get(parent);
        if node.edge_start == NONE {
            &[]
        } else {
            self.edges.slice(node.edge_start, node.edge_len as usize)
        }
    }

    /// Child indices in insertion order.
    pub fn children(&self, parent: u32) -> impl Iterator<Item = u32> + '_ {
        self.edges_of(parent).iter().map(|edge| edge.child)
    }

    /// UCB1 as the C# node computes it: unvisited nodes are always preferred, the root has no
    /// parent term.
    pub fn ucb1(&self, index: u32, exploration_constant: f64) -> f64 {
        let node = self.nodes.get(index);
        let edge = self.edges.get(node.parent_edge);
        if edge.visits == 0 {
            return f64::MAX;
        }
        if node.parent == NONE {
            return edge.average_reward();
        }
        let parent_visits = self.stats(node.parent).visits as f64;
        edge.average_reward()
            + exploration_constant * (parent_visits.ln() / edge.visits as f64).sqrt()
    }

    /// The child of `parent` with the highest UCB1, ties to the first inserted (the C# `MaxBy`);
    /// `NONE` when childless. Same ordering as [`Arena::ucb1`], with the parent's log taken once.
    pub fn select_child(&self, parent: u32, exploration_constant: f64) -> u32 {
        let parent_visits = self.stats(parent).visits as f64;
        let exploration = exploration_constant * parent_visits.ln().sqrt();
        let mut best = NONE;
        let mut best_score = f64::NEG_INFINITY;
        for edge in self.edges_of(parent) {
            let score = if edge.visits == 0 {
                f64::MAX
            } else {
                edge.average_reward() + exploration / (edge.visits as f64).sqrt()
            };
            if score > best_score {
                best = edge.child;
                best_score = score;
            }
        }
        best
    }

    pub fn backpropagate(&mut self, from: u32, reward: f64) {
        let mut current = from;
        loop {
            let node = *self.nodes.get(current);
            let edge = self.edges.get_mut(node.parent_edge);
            edge.visits += 1;
            edge.total_reward += reward;
            if node.parent == NONE {
                return;
            }
            current = node.parent;
        }
    }

    /// Appends the actions from the root down to `node`.
    pub fn path_actions(&self, node: u32, out: &mut Vec<ActionId>) {
        let start = out.len();
        let mut current = node;
        while let Some(action) = self.nodes.get(current).action {
            out.push(action.id());
            current = self.nodes.get(current).parent;
        }
        out[start..].reverse();
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn set(actions: &[Action]) -> ActionSet {
        let mut set = ActionSet::EMPTY;
        for action in actions {
            set.insert(*action);
        }
        set
    }

    #[test]
    fn node_is_48_bytes_and_edge_16() {
        assert_eq!(core::mem::size_of::<Node>(), 48);
        assert_eq!(core::mem::size_of::<Edge>(), 16);
    }

    #[test]
    fn indices_stay_valid_across_chunk_boundaries() {
        let mut arena = Arena::default();
        let root = arena.push_root(CraftingState::default(), set(&[Action::Observe]));
        let mut last = root;
        for _ in 0..CHUNK_ENTRIES + 5 {
            let parent = last;
            arena.get_mut(parent).untried = set(&[Action::Observe]);
            last = arena.push_child(
                parent,
                Action::Observe,
                CraftingState::default(),
                ActionSet::EMPTY,
            );
        }
        assert_eq!(arena.len(), CHUNK_ENTRIES + 6);
        assert_eq!(last as usize, CHUNK_ENTRIES + 5);
        assert_eq!(arena.children(arena.get(last).parent).next(), Some(last));

        arena.clear();
        assert!(arena.is_empty());
        assert_eq!(arena.edge_count(), 0);
        assert_eq!(
            arena.push_root(CraftingState::default(), ActionSet::EMPTY),
            0
        );
    }

    #[test]
    fn edge_slices_never_straddle_a_chunk() {
        let mut edges: Chunked<Edge> = Chunked::default();
        for _ in 0..CHUNK_ENTRIES - 3 {
            edges.push(Edge::default());
        }
        let start = edges.reserve_contiguous(10) as usize;
        assert_eq!(start, CHUNK_ENTRIES, "padded to the next chunk");
        assert_eq!(edges.slice(start as u32, 10).len(), 10);
    }

    #[test]
    fn select_child_agrees_with_ucb1_and_prefers_first_inserted_on_ties() {
        let mut arena = Arena::default();
        let root = arena.push_root(
            CraftingState::default(),
            set(&[Action::BasicSynthesis, Action::BasicTouch]),
        );
        let first = arena.push_child(
            root,
            Action::BasicSynthesis,
            CraftingState::default(),
            ActionSet::EMPTY,
        );
        let second = arena.push_child(
            root,
            Action::BasicTouch,
            CraftingState::default(),
            ActionSet::EMPTY,
        );
        assert!(arena.get(root).is_fully_expanded());
        assert_eq!(arena.children(root).collect::<Vec<_>>(), [first, second]);

        arena.backpropagate(first, 0.5);
        arena.backpropagate(second, 0.5);
        assert_eq!(arena.select_child(root, 1.41), first);
        assert_eq!(arena.stats(root).visits, 2);

        arena.backpropagate(second, 0.9);
        let chosen = arena.select_child(root, 1.41);
        let by_ucb1 = arena
            .children(root)
            .max_by(|a, b| arena.ucb1(*a, 1.41).total_cmp(&arena.ucb1(*b, 1.41)))
            .unwrap();
        assert_eq!(chosen, by_ucb1);
    }
}
