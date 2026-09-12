//! The search tree as an arena: nodes live in fixed-size chunks, links are `u32` indices, and a
//! node's children form an intrusive singly linked list. Nothing here is boxed per node, and a
//! chunk is never moved once allocated, so growth costs one chunk allocation and no copying.

use crate::actions::{Action, ActionId};
use crate::solver::ActionSet;
use crate::state::{CraftStatus, CraftingState};

/// Sentinel for "no node" in every link field.
pub const NONE: u32 = u32::MAX;

const CHUNK_SHIFT: u32 = 16;
/// Nodes per chunk: 65 536 nodes, about 9 MB, so a 10 s search grows in a few dozen steps and a
/// wasm heap grows in pieces it can actually find room for.
pub const CHUNK_NODES: usize = 1 << CHUNK_SHIFT;
const CHUNK_MASK: usize = CHUNK_NODES - 1;

#[derive(Clone, Copy, Debug)]
pub struct Node {
    pub state: CraftingState,
    pub parent: u32,
    pub first_child: u32,
    pub next_sibling: u32,
    /// The action that produced this node; `None` at the root.
    pub action: Option<Action>,
    pub visits: u32,
    pub total_reward: f64,
    pub best_reward: f64,
    pub untried: ActionSet,
}

impl Node {
    fn new(state: CraftingState, parent: u32, action: Option<Action>, untried: ActionSet) -> Node {
        Node {
            state,
            parent,
            first_child: NONE,
            next_sibling: NONE,
            action,
            visits: 0,
            total_reward: 0.0,
            best_reward: f64::MIN,
            untried,
        }
    }

    pub fn is_terminal(&self) -> bool {
        self.state.status != CraftStatus::InProgress
    }

    pub fn is_fully_expanded(&self) -> bool {
        self.untried.is_empty()
    }

    pub fn average_reward(&self) -> f64 {
        if self.visits > 0 {
            self.total_reward / self.visits as f64
        } else {
            0.0
        }
    }
}

#[derive(Clone, Debug, Default)]
pub struct Arena {
    chunks: Vec<Vec<Node>>,
    len: usize,
}

impl Arena {
    /// Drops every node but keeps the chunks for the next solve.
    pub fn clear(&mut self) {
        for chunk in &mut self.chunks {
            chunk.clear();
        }
        self.len = 0;
    }

    pub fn len(&self) -> usize {
        self.len
    }

    pub fn is_empty(&self) -> bool {
        self.len == 0
    }

    pub fn get(&self, index: u32) -> &Node {
        let index = index as usize;
        &self.chunks[index >> CHUNK_SHIFT][index & CHUNK_MASK]
    }

    pub fn get_mut(&mut self, index: u32) -> &mut Node {
        let index = index as usize;
        &mut self.chunks[index >> CHUNK_SHIFT][index & CHUNK_MASK]
    }

    fn push(&mut self, node: Node) -> u32 {
        let chunk = self.len >> CHUNK_SHIFT;
        if chunk == self.chunks.len() {
            self.chunks.push(Vec::with_capacity(CHUNK_NODES));
        }
        self.chunks[chunk].push(node);
        self.len += 1;
        (self.len - 1) as u32
    }

    pub fn push_root(&mut self, state: CraftingState, untried: ActionSet) -> u32 {
        self.push(Node::new(state, NONE, None, untried))
    }

    /// Links the child at the head of the parent's list. Children therefore iterate in reverse
    /// insertion order; callers that must break ties like the C# `MaxBy` (first inserted wins)
    /// keep the last maximum they see.
    pub fn push_child(
        &mut self,
        parent: u32,
        action: Action,
        state: CraftingState,
        untried: ActionSet,
    ) -> u32 {
        let mut node = Node::new(state, parent, Some(action), untried);
        node.next_sibling = self.get(parent).first_child;
        let index = self.push(node);
        self.get_mut(parent).first_child = index;
        index
    }

    pub fn children(&self, parent: u32) -> impl Iterator<Item = u32> + '_ {
        let mut next = self.get(parent).first_child;
        core::iter::from_fn(move || {
            if next == NONE {
                return None;
            }
            let current = next;
            next = self.get(current).next_sibling;
            Some(current)
        })
    }

    /// UCB1 as the C# node computes it: unvisited nodes are always preferred, the root has no
    /// parent term.
    pub fn ucb1(&self, index: u32, exploration_constant: f64) -> f64 {
        let node = self.get(index);
        if node.visits == 0 {
            return f64::MAX;
        }
        if node.parent == NONE {
            return node.average_reward();
        }
        let parent_visits = self.get(node.parent).visits as f64;
        node.average_reward()
            + exploration_constant * (parent_visits.ln() / node.visits as f64).sqrt()
    }

    /// The child of `parent` with the highest UCB1, ties to the first inserted; `NONE` when
    /// childless. Same ordering as [`Arena::ucb1`], with the parent's log taken once per call
    /// instead of once per child.
    pub fn select_child(&self, parent: u32, exploration_constant: f64) -> u32 {
        let exploration = exploration_constant * (self.get(parent).visits as f64).ln().sqrt();
        let mut best = NONE;
        let mut best_score = f64::NEG_INFINITY;
        for child in self.children(parent) {
            let node = self.get(child);
            let score = if node.visits == 0 {
                f64::MAX
            } else {
                node.average_reward() + exploration / (node.visits as f64).sqrt()
            };
            if score >= best_score {
                best = child;
                best_score = score;
            }
        }
        best
    }

    pub fn backpropagate(&mut self, from: u32, reward: f64) {
        let mut current = from;
        while current != NONE {
            let node = self.get_mut(current);
            node.visits += 1;
            node.total_reward += reward;
            node.best_reward = node.best_reward.max(reward);
            current = node.parent;
        }
    }

    /// Appends the actions from the root down to `node`.
    pub fn path_actions(&self, node: u32, out: &mut Vec<ActionId>) {
        let start = out.len();
        let mut current = node;
        while let Some(action) = self.get(current).action {
            out.push(action.id());
            current = self.get(current).parent;
        }
        out[start..].reverse();
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn indices_stay_valid_across_chunk_boundaries() {
        let mut arena = Arena::default();
        let root = arena.push_root(CraftingState::default(), ActionSet::EMPTY);
        let mut last = root;
        for _ in 0..CHUNK_NODES + 5 {
            last = arena.push_child(
                root,
                Action::Observe,
                CraftingState::default(),
                ActionSet::EMPTY,
            );
        }
        assert_eq!(arena.len(), CHUNK_NODES + 6);
        assert_eq!(last as usize, CHUNK_NODES + 5);
        assert_eq!(arena.get(last).parent, root);
        assert_eq!(arena.children(root).next(), Some(last));

        arena.clear();
        assert!(arena.is_empty());
        assert_eq!(
            arena.push_root(CraftingState::default(), ActionSet::EMPTY),
            0
        );
    }

    #[test]
    fn select_child_agrees_with_ucb1_and_prefers_first_inserted_on_ties() {
        let mut arena = Arena::default();
        let root = arena.push_root(CraftingState::default(), ActionSet::EMPTY);
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
        arena.backpropagate(first, 0.5);
        arena.backpropagate(second, 0.5);
        assert_eq!(arena.select_child(root, 1.41), first);

        arena.backpropagate(second, 0.9);
        let chosen = arena.select_child(root, 1.41);
        let by_ucb1 = arena
            .children(root)
            .max_by(|a, b| arena.ucb1(*a, 1.41).total_cmp(&arena.ucb1(*b, 1.41)))
            .unwrap();
        assert_eq!(chosen, by_ucb1);
    }
}
