import React from "react";
import { Spinner } from "@fluentui/react/lib/Spinner";
import { IStackTokens, Stack } from "@fluentui/react/lib/Stack";

const Loading: React.FC = () => {
  const stackTokens: IStackTokens = {
    childrenGap: 20,
    maxWidth: 250,
  };

  return (
    <Stack tokens={stackTokens}>
      <Spinner label="I am definitely loading..." />
    </Stack>
  );
};

export default Loading;
