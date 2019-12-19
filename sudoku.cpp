#include <iostream>
#include <vector>
#include <unordered_set>
#include <iterator>
#include <random>
#include <chrono>

const static int kSize = 9;
typedef int(*Board)[kSize];

void PrintSet(const std::unordered_set<int> & set) {
	std::copy(set.begin(), set.end(), std::ostream_iterator<int>(std::cout, " "));
	puts("");
}

void PrintVector(const std::vector<int> & set) {
	std::copy(set.begin(), set.end(), std::ostream_iterator<int>(std::cout, " "));
	puts("");
}

/// <summary>
/// Print out a nicely formatted version of the board to stdout.
/// </summary>
void PrintBoard(Board board) {
	for (int row = 0; row < kSize; ++row) {
		for (int column = 0; column < kSize; ++column) {
			const int & value = board[row][column];

			if (value == 0) {
				printf("_");
			} else {
				printf("%d", value);
			}

			if (column < kSize - 1) {
				printf(" ");
				if (column % 3 == 2) {
					printf(" ");
				}
			}
		}
		puts("");
		if (row % 3 == 2 && row < kSize - 1) {
			puts("");
		}
	}
}

void Fill(Board board, int value) {
	for (int row = 0; row < kSize; ++row) {
		for (int column = 0; column < kSize; ++column) {
			board[row][column] = value;
		}
	}
}

void Copy(Board from, Board to) {
	std::copy(&from[0][0], &from[0][0] + kSize * kSize, &to[0][0]);
}

void AllowedInRow(Board board, std::unordered_set<int> * const values, int row) {
	for (const int & x : board[row]) {
		values->erase(x);
	}
}

void AllowedInColumn(Board board, std::unordered_set<int> * const values, int column) {
	for (int row = 0; row < kSize; ++row) {
		values->erase(board[row][column]);
	}
}

void AllowedInBox(Board board, std::unordered_set<int> * const values, int row, int column) {
	for (int i = 0; i < 3; ++i) {
		for (int j = 0; j < 3; ++j) {
			values->erase(board[row - (row % 3) + i][column - (column % 3) + j]);
		}
	}
}

/// <summary>
/// Gets a shuffled vector containing possible values for this cell on the board.
/// </summary>
const std::vector<int> * GetAllowedValues(Board board, int row, int column) {
	static std::unordered_set<int> allowed;
	allowed = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

	AllowedInRow(board, &allowed, row);
	AllowedInColumn(board, &allowed, column);
	AllowedInBox(board, &allowed, row, column);

	printf("Allowed: ");
	PrintSet(allowed);

	static std::vector<int> shuffled;
	shuffled = std::vector<int>(allowed.begin(), allowed.end());
	std::shuffle(shuffled.begin(), shuffled.end(), std::default_random_engine((int)std::chrono::system_clock::now().time_since_epoch().count()));

	return &shuffled;

}

/// <summary>
/// Returns true if row-column position is at bottom right of board.
/// </summary>
bool IsAtBottomRight(int row, int column) {
	return row >= kSize - 1 && column >= kSize - 1;
}

/// <summary>
/// Get next row-column position.
/// </summary>
std::pair<int, int> IterateNext(int row, int column) {
	if (column < kSize - 1) {
		return std::pair<int, int>(row, column + 1);
	} else {
		return std::pair<int, int>(row + 1, 0);
	}
}

enum class RecursionResult {
	kFailed,
	kComplete
};

/// <summary>
/// Recursively create a filled sudoku board.
/// </summary>
/// <param name="old_state">State of board from previous branch.</param>
/// <param name="output">Board to write to when complete.</param>
/// <param name="row">Current row of this branch.</param>
/// <param name="column">Current column of this branch.</param>
/// <returns>Whether operation completed successfully.</returns>
RecursionResult CreateRecursive(Board old_state, Board output, int row, int column) {

	Board state = new int[kSize][kSize];
	Copy(old_state, state);

	printf("\n\n[ Board at (%d, %d) ]\n", row, column);
	PrintBoard(state);

	std::vector<int> allowed(*GetAllowedValues(old_state, row, column));
	printf("Shuffle: ");
	PrintVector(allowed);

	if (allowed.size() == 0) {
		puts("No possible");
		// This branch had no possible numbers. Take step back toward beginning.
		delete[] state;
		return RecursionResult::kFailed;

	} else {
		auto next_pos = IterateNext(row, column);

		for (const auto & val : allowed) {
			state[row][column] = val;
			printf("Attempting with %d\n", val);

			if (IsAtBottomRight(row, column)) {
				puts("BOARD COMPLETED");
				// Board was completed successfully.
				Copy(state, output);
				delete[] state;
				return RecursionResult::kComplete;

			} else {

				auto ret_code = CreateRecursive(state, output, next_pos.first, next_pos.second);

				if (ret_code == RecursionResult::kComplete) {
					// The tip of this branch completed successfully. Cleanup and echo completion status toward beginning.
					delete[] state;
					return RecursionResult::kComplete;
				}

			}

		}

		// This branch was a dead end. Step upward.
		delete[] state;
		return RecursionResult::kFailed;
	}

}

void Run() {

	Board board = new int[kSize][kSize];
	Fill(board, 0);

	puts("orig");
	PrintBoard(board);

	CreateRecursive(board, board, 0, 0);

	puts("\nFINAL");
	PrintBoard(board);

	delete[] board;

}

int main() {
	Run();
	printf("\n\n\n=========== END ===========");
	return 0;
}