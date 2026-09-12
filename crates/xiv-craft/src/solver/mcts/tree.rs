//! The search tree as an arena: nodes live in one `Vec`, links are `u32` indices, and a node's
//! children form an intrusive singly linked list. Nothing here is boxed, so an iteration allocates
//! only when the arena doubles.

use crate::actions::{Action, ActionId};
use crate::solver::ActionSet;
use crate::state::{CraftStatus, CraftingState};

/// Sentinel for "no node" in every link field.
pub const NONE: u32 = u32::MAX;

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
    nodes: Vec<Node>,
}

impl Arena {
    pub fn with_capacity(nodes: usize) -> Arena {
        Arena {
            nodes: Vec::with_capacity(nodes),
        }
    }

    /// Drops every node but keeps the allocation for the next solve.
    pub fn clear(&mut self) {
        self.nodes.clear();
    }

    pub fn len(&self) -> usize {
        self.nodes.len()
    }

    pub fn is_empty(&self) -> bool {
        self.nodes.is_empty()
    }

    pub fn get(&self, index: u32) -> &Node {
        &self.nodes[index as usize]
    }

    pub fn get_mut(&mut self, index: u32) -> &mut Node {
        &mut self.nodes[index as usize]
    }

    pub fn push_root(&mut self, state: CraftingState, untried: ActionSet) -> u32 {
        self.nodes.push(Node::new(state, NONE, None, untried));
        (self.nodes.len() - 1) as u32
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
        let index = self.nodes.len() as u32;
        let mut node = Node::new(state, parent, Some(action), untried);
        node.next_sibling = self.nodes[parent as usize].first_child;
        self.nodes.push(node);
        self.nodes[parent as usize].first_child = index;
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
