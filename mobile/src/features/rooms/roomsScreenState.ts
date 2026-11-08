export type RoomScreenMode = "initialLoading" | "error" | "empty" | "list";

export interface RoomsScreenState {
  mode: RoomScreenMode;
  showCreateBar: boolean;
  showEmptyAction: boolean;
  showInlineError: boolean;
}

export function resolveRoomsScreenState({
  hasCompletedInitialLoad,
  isLoading,
  error,
  roomCount,
}: {
  hasCompletedInitialLoad: boolean;
  isLoading: boolean;
  error: string | null;
  roomCount: number;
}): RoomsScreenState {
  if (!hasCompletedInitialLoad) {
    return {
      mode: "initialLoading",
      showCreateBar: false,
      showEmptyAction: false,
      showInlineError: false,
    };
  }

  if (error) {
    if (roomCount === 0) {
      return {
        mode: "error",
        showCreateBar: false,
        showEmptyAction: false,
        showInlineError: false,
      };
    }
    return {
      mode: "list",
      showCreateBar: true,
      showEmptyAction: false,
      showInlineError: true,
    };
  }

  if (isLoading && roomCount === 0) {
    return {
      mode: "initialLoading",
      showCreateBar: false,
      showEmptyAction: false,
      showInlineError: false,
    };
  }

  if (roomCount === 0) {
    return {
      mode: "empty",
      showCreateBar: false,
      showEmptyAction: true,
      showInlineError: false,
    };
  }

  return {
    mode: "list",
    showCreateBar: true,
    showEmptyAction: false,
    showInlineError: false,
  };
}
